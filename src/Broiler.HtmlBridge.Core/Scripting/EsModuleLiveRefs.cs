using System;
using System.Collections.Generic;

namespace Broiler.HtmlBridge.Internal.Scripting;

/// <summary>
/// Rewrites read-references of a module's <b>named</b> imports into live reads from the exporting module's
/// registry object (Phase 7 item 6), so a value reassigned in the exporter after evaluation — the canonical
/// live-binding case (an exported counter mutated by an exported function) — is observed through the named
/// import rather than a stale snapshot. It pairs with the linker's live <c>export</c> accessors (getters on
/// the exports object): the exporter publishes live getters, and this pass makes the importer read through
/// them each time.
/// </summary>
/// <remarks>
/// <para>The pass is <b>scope-accurate but sound-biased</b>. It lexes the module (string/template/comment/
/// regex aware) tracking the constructs that can (re)bind a name — <c>var</c>/<c>let</c>/<c>const</c>
/// declarations and their destructuring patterns, function/arrow/<c>catch</c> parameters, and function
/// names. An imported local that appears in <b>any</b> binding position anywhere in the module is left as a
/// snapshot (a nearer binding could shadow the import in some scope, so a live rewrite would be wrong); an
/// imported local that is <b>never</b> bound anywhere is free in every scope, so each of its plain
/// read-references is rewritten to the live getter access. Classification is deliberately conservative:
/// wherever an occurrence's role is ambiguous the pass marks the name unrewritable (snapshot) rather than
/// risk a wrong rewrite, and it keeps the linker's <c>var</c> snapshot bindings as the safety net for any
/// reference it chooses not to rewrite. A module containing <c>class</c>, <c>with</c> or <c>eval</c> — whose
/// scoping this lexer will not attempt — falls back to snapshot entirely (returns <c>null</c>). Namespace
/// imports (<c>import * as ns</c>) need no rewriting (they read the live exports object member-wise) and
/// default imports are values, not live bindings, so neither is handled here.</para>
/// </remarks>
internal static class EsModuleLiveRefs
{
    /// <summary>An in-place replacement of a source span with <paramref name="Text"/>.</summary>
    internal readonly record struct RefEdit(int Start, int Length, string Text);

    private sealed class ParenFrame
    {
        public bool IsParams;                 // opened as a function/catch parameter list
        public readonly List<string> Names = new(); // imported names seen inside (for arrow-param detection)
    }

    /// <summary>
    /// Returns the read-reference edits for the named imports in <paramref name="namedLocals"/> (local name →
    /// its live read expression, e.g. <c>__src0["imported"]</c>), or <c>null</c> when the module keeps snapshot
    /// bindings (nothing rewritable, or a construct this lexer will not scope). <paramref name="skip"/> lists
    /// source spans to ignore (the import statements and stripped export statements — their identifiers are
    /// binding declarations, not references).
    /// </summary>
    public static List<RefEdit>? TryBuild(
        string src, IReadOnlyDictionary<string, string> namedLocals, IReadOnlyList<(int Start, int End)> skip)
    {
        if (namedLocals.Count == 0)
            return null;

        var notRewritable = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<(int Start, int Len, string Name)>();

        int n = src.Length, i = 0, depth = 0;
        char prev = '\0', prev2 = '\0';

        bool inDecl = false; int declDepth = 0; bool declBinding = false; // var/let/const state
        bool pendingParams = false;   // next '(' is a function/catch parameter list
        bool pendingFuncName = false; // the identifier right after `function` is its name
        int paramsActive = 0;         // number of enclosing parameter-list frames
        var parens = new List<ParenFrame>();
        List<string>? lastParenNames = null; // names of the most recently closed non-params '(' group

        bool InSkip(int pos)
        {
            foreach (var (s, e) in skip)
                if (pos >= s && pos < e) return true;
            return false;
        }

        while (i < n)
        {
            char c = src[i];

            if (c is ' ' or '\t' or '\r' or '\n' or '\f' or '\v') { i++; continue; }
            if (c == '/' && i + 1 < n && src[i + 1] == '/') { i += 2; while (i < n && src[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < n && src[i + 1] == '*') { i += 2; while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++; i = Math.Min(n, i + 2); continue; }
            if (c is '"' or '\'') { i = EsModuleScanner.SkipString(src, i, c); prev2 = prev; prev = c; pendingParams = pendingFuncName = false; continue; }
            if (c == '`') { i = EsModuleScanner.SkipTemplate(src, i); prev2 = prev; prev = '`'; pendingParams = pendingFuncName = false; continue; }
            if (c == '/' && EsModuleScanner.RegexAllowed(prev)) { i = EsModuleScanner.SkipRegex(src, i); prev2 = prev; prev = '/'; pendingParams = pendingFuncName = false; continue; }

            // Arrow: `(...) =>` params (single-ident `x =>` is caught at the identifier). Mark the just-closed
            // parenthesised group's imported names as bindings.
            if (c == '=' && i + 1 < n && src[i + 1] == '>')
            {
                if (prev == ')' && lastParenNames != null)
                    foreach (var nm in lastParenNames) notRewritable.Add(nm);
                lastParenNames = null;
                prev2 = prev; prev = '>'; i += 2; pendingParams = pendingFuncName = false;
                continue;
            }

            if (EsModuleScanner.IsIdentStart(c))
            {
                int s = i;
                i++;
                while (i < n && EsModuleScanner.IsIdentPart(src[i])) i++;
                var word = src[s..i];

                if (word is "class" or "with" or "eval")
                    return null; // scoping we will not attempt → whole-module snapshot

                bool consumedFuncName = false;
                if (pendingFuncName)
                {
                    // The name in `function NAME(...)` (declaration or named expression) is a binding.
                    if (namedLocals.ContainsKey(word) && !InSkip(s)) notRewritable.Add(word);
                    pendingFuncName = false;
                    consumedFuncName = true;
                }

                if (word is "var" or "let" or "const")
                {
                    inDecl = true; declDepth = depth; declBinding = true;
                }
                else if (word == "function")
                {
                    pendingFuncName = true; pendingParams = true;
                }
                else if (word == "catch")
                {
                    pendingParams = true;
                }
                else
                {
                    if (!consumedFuncName)
                        pendingParams = false; // any other token ends a pending param-list expectation
                }

                if (!consumedFuncName && namedLocals.TryGetValue(word, out var liveRead) && !InSkip(s))
                {
                    if (parens.Count > 0) parens[^1].Names.Add(word); // for arrow-param detection

                    bool member = prev == '.' && prev2 != '.'; // obj.name (not spread ...name)
                    if (!member)
                    {
                        if ((inDecl && declBinding) || paramsActive > 0)
                        {
                            notRewritable.Add(word); // binding position
                        }
                        else
                        {
                            char next = NextSignificant(src, i);
                            if (next == '=' && NextIsArrow(src, i))
                            {
                                notRewritable.Add(word); // single-parameter arrow: `name => …`
                            }
                            else if (next == ':')
                            {
                                // property key / label / ternary-then — ambiguous; leave (snapshot).
                            }
                            else if (next == '=' && IsSimpleAssign(src, i))
                            {
                                notRewritable.Add(word); // assignment / default target
                            }
                            else if ((prev == '{' || prev == ',') && (next == '}' || next == ','))
                            {
                                // object-literal shorthand / pattern slot — cannot rewrite to a computed key; leave.
                            }
                            else
                            {
                                candidates.Add((s, word.Length, word)); // plain read-reference
                            }
                        }
                    }
                }

                prev2 = prev; prev = word[^1];
                continue;
            }

            // Punctuation / operators.
            switch (c)
            {
                case '(':
                    var f = new ParenFrame { IsParams = pendingParams };
                    if (pendingParams) paramsActive++;
                    parens.Add(f);
                    pendingParams = false; pendingFuncName = false;
                    depth++;
                    break;
                case '[':
                case '{':
                    depth++;
                    pendingParams = false;
                    break;
                case ')':
                    depth--;
                    if (parens.Count > 0)
                    {
                        var top = parens[^1];
                        parens.RemoveAt(parens.Count - 1);
                        if (top.IsParams) { if (paramsActive > 0) paramsActive--; lastParenNames = null; }
                        else lastParenNames = top.Names; // candidate arrow param group
                    }
                    if (inDecl && depth < declDepth) inDecl = false;
                    break;
                case ']':
                case '}':
                    depth--;
                    if (inDecl && depth < declDepth) inDecl = false;
                    break;
                case ';':
                    if (inDecl && depth == declDepth) inDecl = false;
                    break;
                case '=':
                    // A simple '=' at the declaration's own depth flips a var/let/const from its binding
                    // pattern to its initializer expression; a top-level ',' flips it back.
                    if (inDecl && depth == declDepth && IsSimpleAssign(src, i - 0)) declBinding = false;
                    break;
                case ',':
                    if (inDecl && depth == declDepth) declBinding = true;
                    break;
            }

            // A pending function/catch parameter list only survives a generator `*`; any other token ends it.
            // (`=>` is handled above and never reaches here.)
            pendingParams = pendingParams && c == '*';
            prev2 = prev; prev = c; i++;
        }

        if (candidates.Count == 0)
            return null;

        var edits = new List<RefEdit>();
        foreach (var (start, len, name) in candidates)
            if (!notRewritable.Contains(name))
                edits.Add(new RefEdit(start, len, namedLocals[name]));

        return edits.Count > 0 ? edits : null;
    }

    /// <summary>The next significant (non-whitespace, non-comment) char at or after <paramref name="i"/>, or '\0'.</summary>
    private static char NextSignificant(string src, int i)
    {
        int j = EsModuleScanner.SkipTrivia(src, i);
        return j < src.Length ? src[j] : '\0';
    }

    /// <summary>True when the next significant token is a <c>=&gt;</c> arrow (so the preceding identifier is a
    /// single arrow parameter, e.g. <c>name =&gt; …</c>).</summary>
    private static bool NextIsArrow(string src, int i)
    {
        int j = EsModuleScanner.SkipTrivia(src, i);
        return j + 1 < src.Length && src[j] == '=' && src[j + 1] == '>';
    }

    /// <summary>
    /// True when the next significant char is a simple assignment <c>=</c> (an assignment or default target),
    /// not part of <c>==</c>/<c>===</c>/<c>=&gt;</c>/<c>&lt;=</c>/<c>&gt;=</c>/<c>!=</c>.
    /// </summary>
    private static bool IsSimpleAssign(string src, int i)
    {
        int j = EsModuleScanner.SkipTrivia(src, i);
        if (j >= src.Length || src[j] != '=') return false;
        char before = j > 0 ? src[j - 1] : '\0';
        char after = j + 1 < src.Length ? src[j + 1] : '\0';
        return before is not ('=' or '!' or '<' or '>') && after is not ('=' or '>');
    }
}
