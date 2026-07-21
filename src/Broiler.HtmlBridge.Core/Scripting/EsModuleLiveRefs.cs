using System;
using System.Collections.Generic;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// Rewrites read-references of a module's <b>named</b> imports into live reads from the exporting module's
/// registry object (Phase 7 item 6), so that a value reassigned in the exporter after evaluation — the
/// canonical live-binding case, e.g. an exported counter mutated by an exported function — is observed
/// through the named import rather than a stale snapshot. It pairs with the linker's live <c>export</c>
/// accessors (getters on the exports object): the exporter publishes live getters, and this pass makes the
/// importer read through them each time.
/// </summary>
/// <remarks>
/// <para>This is <b>not</b> a scope-accurate rewriter — it is a conservative lexer that is <em>sound by
/// abdication</em>: it rewrites only occurrences it can prove are plain read-references of an imported
/// name, and it <b>aborts the whole module</b> (leaving the linker's snapshot bindings, which are correct
/// but non-live) the moment it meets anything that could make a bare identifier something other than a
/// read of that import — a binding/scope construct (<c>function</c>, <c>=&gt;</c>, <c>class</c>,
/// <c>catch</c>, <c>var</c>/<c>let</c>/<c>const</c>) or an ambiguous position (a property key, a shorthand
/// or destructuring slot, an assignment target, a spread). So a mis-analysis can only ever fall back to the
/// existing snapshot behaviour, never emit wrong code. Namespace imports (<c>import * as ns</c>) need no
/// rewriting — they already read the live exports object member-wise — and default imports are values, not
/// live bindings, so neither is handled here.</para>
/// </remarks>
internal static class EsModuleLiveRefs
{
    /// <summary>An in-place replacement of a source span with <paramref name="Text"/>.</summary>
    internal readonly record struct RefEdit(int Start, int Length, string Text);

    /// <summary>
    /// Returns the read-reference edits for the named imports in <paramref name="namedLocals"/>
    /// (local name → its live read expression, e.g. <c>__src0["imported"]</c>), or <c>null</c> to signal the
    /// module must keep snapshot bindings. <paramref name="skip"/> lists source spans to ignore (the import
    /// statements and the stripped export statements, whose identifiers are binding declarations, not reads).
    /// </summary>
    public static List<RefEdit>? TryBuild(
        string src, IReadOnlyDictionary<string, string> namedLocals, IReadOnlyList<(int Start, int End)> skip)
    {
        if (namedLocals.Count == 0)
            return null; // nothing to rewrite

        var edits = new List<RefEdit>();
        int n = src.Length;
        int i = 0;
        char prev = '\0';       // last significant char
        char prev2 = '\0';      // significant char before that (for `...` spread detection)

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
            if (c is '"' or '\'') { i = EsModuleScanner.SkipString(src, i, c); prev2 = prev; prev = c; continue; }
            if (c == '`') { i = EsModuleScanner.SkipTemplate(src, i); prev2 = prev; prev = '`'; continue; }
            if (c == '/' && EsModuleScanner.RegexAllowed(prev)) { i = EsModuleScanner.SkipRegex(src, i); prev2 = prev; prev = '/'; continue; }

            // Arrow function — introduces a parameter scope we cannot analyse. Abort.
            if (c == '=' && i + 1 < n && src[i + 1] == '>') { return null; }

            if (EsModuleScanner.IsIdentStart(c))
            {
                int s = i;
                i++;
                while (i < n && EsModuleScanner.IsIdentPart(src[i])) i++;
                var word = src[s..i];

                // Any construct that could rebind a name or open a scope we cannot reason about → abort.
                if (word is "function" or "class" or "catch" or "var" or "let" or "const" or "eval" or "with")
                    return null;

                if (namedLocals.TryGetValue(word, out var liveRead) && !InSkip(s))
                {
                    // Property access `obj.name` (prev '.') — the name is a member, not the import — leave it;
                    // but a spread `...name` (prev '...') IS a read we cannot rewrite via that path → abort.
                    if (prev == '.')
                    {
                        if (prev2 == '.') return null; // `...name`
                        // single-dot member access: not the import binding; skip
                    }
                    else
                    {
                        char next = NextSignificant(src, i);
                        // Property key / label / ternary-else `name:`; assignment or default `name =`;
                        // object-shorthand or pattern slot `{name}` / `,name,`. Any of these → abort.
                        if (next == ':') return null;
                        if (next == '=' && !IsCompoundOrCompare(src, i, next)) return null;
                        if ((prev == '{' || prev == ',') && (next == '}' || next == ',')) return null;

                        edits.Add(new RefEdit(s, word.Length, liveRead));
                    }
                }

                prev2 = prev;
                prev = word[^1];
                continue;
            }

            prev2 = prev;
            prev = c;
            i++;
        }

        return edits.Count > 0 ? edits : null;
    }

    /// <summary>The next significant (non-whitespace, non-comment) char at or after <paramref name="i"/>, or '\0'.</summary>
    private static char NextSignificant(string src, int i)
    {
        int j = EsModuleScanner.SkipTrivia(src, i);
        return j < src.Length ? src[j] : '\0';
    }

    /// <summary>
    /// True when the '=' at the next-significant position is part of a comparison (<c>==</c>, <c>===</c>) or
    /// an arrow (<c>=&gt;</c>) rather than a simple assignment — those are safe reads, a bare <c>=</c> is not.
    /// </summary>
    private static bool IsCompoundOrCompare(string src, int i, char next)
    {
        int j = EsModuleScanner.SkipTrivia(src, i); // position of '='
        if (j + 1 >= src.Length) return false;
        char after = src[j + 1];
        return after is '=' or '>';
    }
}
