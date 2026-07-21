using System;
using System.Collections.Generic;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// A focused scanner (Phase 7 item 6) that finds a module's <b>top-level</b> static <c>import</c>/
/// <c>export</c> statements so the <see cref="EsModuleLinker"/> can rewrite them into a runtime registry.
/// It is deliberately not a full JS parser: it tracks string / template / comment / regex literals and
/// bracket nesting so it never mistakes an <c>import</c>/<c>export</c> inside a string, a comment, a
/// property name (<c>obj.export</c>), <c>exports.x</c> (CommonJS), or a nested scope for a module
/// statement, and it recognises only the import/export forms it can transform correctly. Any unrecognised
/// or malformed top-level form sets <see cref="EsModuleSyntax.Supported"/> to <c>false</c>, and the linker
/// then leaves that module untransformed rather than emitting wrong code — so the feature is additive.
/// </summary>
/// <remarks>
/// <c>import.meta</c> is recognised (at any depth) and its spans recorded for the linker to rewrite into a
/// per-module meta object. Dynamic <c>import(...)</c> is recognised (at any depth): its keyword span is
/// recorded so the linker can route it through the module graph, and a single string-literal argument is
/// captured so the graph loader can resolve/fetch/link that dependency. Deliberately out of scope (marks the
/// module unsupported, or is left as-is): destructuring exports (<c>export const { a } = o</c>) and top-level
/// <c>await</c>.
/// </remarks>
internal static class EsModuleScanner
{
    public static EsModuleSyntax Scan(string source)
    {
        var syntax = new EsModuleSyntax();
        int n = source.Length;
        int i = 0;
        int depth = 0;              // () [] {} nesting
        char prevSignificant = '\0'; // last non-ws/comment char (for regex detection + property guard)

        while (i < n)
        {
            char c = source[i];

            // Whitespace.
            if (c is ' ' or '\t' or '\r' or '\n' or '\f' or '\v')
            {
                i++;
                continue;
            }

            // Comments.
            if (c == '/' && i + 1 < n && source[i + 1] == '/')
            {
                i += 2;
                while (i < n && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < n && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(n, i + 2);
                continue;
            }

            // String literals.
            if (c is '"' or '\'')
            {
                i = SkipString(source, i, c);
                prevSignificant = c;
                continue;
            }

            // Template literals (with ${ } interpolation; nesting-aware).
            if (c == '`')
            {
                i = SkipTemplate(source, i);
                prevSignificant = '`';
                continue;
            }

            // Regex literal vs division: only when a regex is grammatically allowed here.
            if (c == '/' && RegexAllowed(prevSignificant))
            {
                i = SkipRegex(source, i);
                prevSignificant = '/';
                continue;
            }

            // Bracket nesting.
            if (c is '(' or '[' or '{') { depth++; prevSignificant = c; i++; continue; }
            if (c is ')' or ']' or '}') { if (depth > 0) depth--; prevSignificant = c; i++; continue; }

            // `import` — a statement only at top level, but `import.meta` and dynamic `import(...)` are
            // expressions recognised at any bracket depth.
            if (c == 'i' && prevSignificant != '.' &&
                IsWordAt(source, i, out int importEnd) == "import" &&
                (i == 0 || !IsIdentPart(source[i - 1])))
            {
                int p = SkipTrivia(source, importEnd);
                if (p < n && source[p] == '.')
                {
                    // import.meta (the only valid meta-property): record its span so the linker can replace
                    // it with a synthesized per-module meta object.
                    int metaStart = SkipTrivia(source, p + 1);
                    if (StartsWord(source, metaStart, "meta"))
                    {
                        int metaEnd = metaStart + 4;
                        syntax.ImportMetaSpans.Add((i, metaEnd - i));
                        prevSignificant = 'a'; // ident char: a following '/' is division, '.' is member access
                        i = metaEnd;
                        continue;
                    }
                    // import.<other> — not a standard meta-property; leave the keyword untouched.
                    prevSignificant = 't';
                    i = importEnd;
                    continue;
                }
                if (p < n && source[p] == '(')
                {
                    // Dynamic import(...) — record the keyword span (the linker rewrites it to a graph-backed
                    // loader) and the argument if it is a single string literal (so the graph loader can
                    // resolve/fetch/link that dependency ahead of time).
                    var litSpec = TryReadDynamicImportLiteral(source, p);
                    syntax.DynamicImports.Add(new DynamicImport(i, importEnd - i, litSpec));
                    prevSignificant = 't';
                    i = importEnd;
                    continue;
                }
                if (depth == 0)
                {
                    if (!ParseImport(source, i, importEnd, syntax, out i))
                    {
                        syntax.Supported = false;
                        return syntax;
                    }
                    prevSignificant = ';';
                    continue;
                }
                // An `import` statement is invalid below top level; leave it (the module runs as-is).
                prevSignificant = 't';
                i = importEnd;
                continue;
            }

            // Only top-level export statements are module syntax.
            if (depth == 0 && c == 'e' && prevSignificant != '.' &&
                IsWordAt(source, i, out int exportEnd) == "export" &&
                (i == 0 || !IsIdentPart(source[i - 1])))
            {
                if (!ParseExport(source, i, exportEnd, syntax, out i))
                {
                    syntax.Supported = false;
                    return syntax;
                }
                prevSignificant = ';';
                continue;
            }

            prevSignificant = c;
            i++;
        }

        return syntax;
    }

    /// <summary>
    /// Reads the argument of a dynamic <c>import(</c> when it is a single string literal, returning its
    /// value; returns <c>null</c> for a runtime-computed argument (e.g. <c>import("a"+x)</c>) or attributes
    /// that make the first argument non-literal. <paramref name="parenPos"/> is the index of the <c>(</c>.
    /// </summary>
    private static string? TryReadDynamicImportLiteral(string src, int parenPos)
    {
        int i = SkipTrivia(src, parenPos + 1);
        if (i >= src.Length || src[i] is not ('"' or '\'')) return null;
        if (!ReadStringLiteral(src, i, out var spec, out int after)) return null;
        int j = SkipTrivia(src, after);
        // A pure literal specifier: the first argument ends here — either the call closes ')' or a second
        // argument (import options) follows ','. Anything else (operator, template, etc.) is computed.
        return j < src.Length && src[j] is ')' or ',' ? spec : null;
    }

    // ── import ──────────────────────────────────────────────────────────────

    private static bool ParseImport(string src, int stmtStart, int afterKw, EsModuleSyntax syntax, out int next)
    {
        next = afterKw;
        int i = SkipTrivia(src, afterKw);
        int n = src.Length;
        if (i >= n) return false;

        // Side-effect import: import "spec";
        if (src[i] is '"' or '\'')
        {
            if (!ReadStringLiteral(src, i, out var spec, out int afterSpec)) return false;
            int end = ConsumeToStatementEnd(src, afterSpec);
            syntax.Imports.Add(new ModuleImport(spec, null, null, [], stmtStart, end - stmtStart));
            next = end;
            return true;
        }

        string? defaultName = null;
        string? namespaceName = null;
        var named = new List<(string, string)>();

        // default binding
        if (IsIdentStart(src[i]))
        {
            defaultName = ReadIdent(src, ref i);
            i = SkipTrivia(src, i);
            if (i < n && src[i] == ',') { i = SkipTrivia(src, i + 1); }
            else
            {
                // must be `from`
                if (!ExpectWord(src, ref i, "from")) return false;
                return FinishImport(src, i, stmtStart, defaultName, null, named, syntax, out next);
            }
        }

        if (i < n && src[i] == '*')
        {
            i = SkipTrivia(src, i + 1);
            if (!ExpectWord(src, ref i, "as")) return false;
            i = SkipTrivia(src, i);
            if (i >= n || !IsIdentStart(src[i])) return false;
            namespaceName = ReadIdent(src, ref i);
            i = SkipTrivia(src, i);
        }
        else if (i < n && src[i] == '{')
        {
            if (!ReadNamedList(src, ref i, named)) return false;
            i = SkipTrivia(src, i);
        }
        else
        {
            return false;
        }

        if (!ExpectWord(src, ref i, "from")) return false;
        return FinishImport(src, i, stmtStart, defaultName, namespaceName, named, syntax, out next);
    }

    private static bool FinishImport(
        string src, int i, int stmtStart, string? defaultName, string? namespaceName,
        List<(string, string)> named, EsModuleSyntax syntax, out int next)
    {
        next = i;
        i = SkipTrivia(src, i);
        if (i >= src.Length || src[i] is not ('"' or '\'')) return false;
        if (!ReadStringLiteral(src, i, out var spec, out int afterSpec)) return false;
        int end = ConsumeToStatementEnd(src, afterSpec);
        syntax.Imports.Add(new ModuleImport(spec, defaultName, namespaceName, named, stmtStart, end - stmtStart));
        next = end;
        return true;
    }

    // ── export ──────────────────────────────────────────────────────────────

    private static bool ParseExport(string src, int stmtStart, int afterKw, EsModuleSyntax syntax, out int next)
    {
        next = afterKw;
        int n = src.Length;
        int i = SkipTrivia(src, afterKw);
        if (i >= n) return false;

        // export * [as ns] from "spec"
        if (src[i] == '*')
        {
            i = SkipTrivia(src, i + 1);
            string? nsName = null;
            if (StartsWord(src, i, "as"))
            {
                i = SkipTrivia(src, i + 2);
                if (i >= n || !IsIdentStart(src[i])) return false;
                nsName = ReadIdent(src, ref i);
                i = SkipTrivia(src, i);
            }
            if (!ExpectWord(src, ref i, "from")) return false;
            i = SkipTrivia(src, i);
            if (!ReadStringLiteral(src, i, out var spec, out int afterSpec)) return false;
            int end = ConsumeToStatementEnd(src, afterSpec);
            syntax.Exports.Add(nsName == null
                ? new ModuleExport(ModuleExportKind.ReExportStar, stmtStart, end - stmtStart, Specifier: spec)
                : new ModuleExport(ModuleExportKind.ReExportStarAs, stmtStart, end - stmtStart, Specifier: spec, NamespaceName: nsName));
            next = end;
            return true;
        }

        // export { ... } [from "spec"]
        if (src[i] == '{')
        {
            var pairs = new List<(string, string)>();
            if (!ReadNamedList(src, ref i, pairs)) return false;
            i = SkipTrivia(src, i);
            if (StartsWord(src, i, "from"))
            {
                i = SkipTrivia(src, i + 4);
                if (!ReadStringLiteral(src, i, out var spec, out int afterSpec)) return false;
                int end = ConsumeToStatementEnd(src, afterSpec);
                // For re-export, the pair is (imported, exported).
                syntax.Exports.Add(new ModuleExport(ModuleExportKind.ReExportNamed, stmtStart, end - stmtStart,
                    Specifier: spec, Bindings: pairs));
                next = end;
                return true;
            }
            else
            {
                int end = ConsumeToStatementEnd(src, i);
                // For a local list, the pair is (local, exported).
                syntax.Exports.Add(new ModuleExport(ModuleExportKind.NamedList, stmtStart, end - stmtStart,
                    Bindings: pairs));
                next = end;
                return true;
            }
        }

        // export default ...
        if (StartsWord(src, i, "default"))
        {
            int afterDefault = i + 7; // "default"
            // The rewrite span covers only `export ... default`; the expression/decl stays in place.
            syntax.Exports.Add(new ModuleExport(ModuleExportKind.Default, stmtStart, afterDefault - stmtStart));
            next = afterDefault;
            return true;
        }

        // export const/let/var/function/class/async function NAME ...
        var names = new List<string>();
        int declKwStart = i;
        string? kw = IsWordAt(src, i, out int kwEnd) ;
        if (kw is "function" or "class")
        {
            i = SkipTrivia(src, kwEnd);
            if (kw == "function" && i < n && src[i] == '*') i = SkipTrivia(src, i + 1); // generator
            if (i >= n || !IsIdentStart(src[i])) return false;
            names.Add(ReadIdent(src, ref i));
        }
        else if (kw == "async")
        {
            i = SkipTrivia(src, kwEnd);
            if (!StartsWord(src, i, "function")) return false;
            i = SkipTrivia(src, i + 8);
            if (i < n && src[i] == '*') i = SkipTrivia(src, i + 1);
            if (i >= n || !IsIdentStart(src[i])) return false;
            names.Add(ReadIdent(src, ref i));
        }
        else if (kw is "const" or "let" or "var")
        {
            if (!CollectDeclaredNames(src, kwEnd, names)) return false;
        }
        else
        {
            return false; // unsupported export form
        }

        // Strip only `export ` (keyword + following trivia up to the declaration keyword); the declaration
        // stays and its exported names are assigned at module end by the linker.
        syntax.Exports.Add(new ModuleExport(ModuleExportKind.Declaration, stmtStart, declKwStart - stmtStart,
            Names: names));
        next = declKwStart;
        return true;
    }

    /// <summary>
    /// Collects the simple binding identifiers of a <c>const/let/var</c> declaration (the name after the
    /// keyword and after each top-level comma), scanning nesting-aware to the declaration's terminating
    /// <c>;</c> or EOF. Returns <c>false</c> for a destructuring pattern (an unsupported form).
    /// </summary>
    private static bool CollectDeclaredNames(string src, int afterKw, List<string> names)
    {
        int n = src.Length;
        int i = SkipTrivia(src, afterKw);
        bool expectName = true;
        int depth = 0;
        while (i < n)
        {
            char c = src[i];
            if (expectName && depth == 0)
            {
                if (c is '{' or '[') return false; // destructuring — unsupported
                if (!IsIdentStart(c)) return false;
                names.Add(ReadIdent(src, ref i));
                expectName = false;
                continue;
            }

            // Skip strings/templates/comments/regex inside initializers so commas/semicolons there don't count.
            if (c is '"' or '\'') { i = SkipString(src, i, c); continue; }
            if (c == '`') { i = SkipTemplate(src, i); continue; }
            if (c == '/' && i + 1 < n && src[i + 1] == '/') { while (i < n && src[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < n && src[i + 1] == '*') { i += 2; while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++; i = Math.Min(n, i + 2); continue; }

            if (c is '(' or '[' or '{') { depth++; i++; continue; }
            if (c is ')' or ']' or '}') { if (depth > 0) depth--; i++; continue; }

            if (depth == 0)
            {
                if (c == ';') return true;
                if (c == ',') { expectName = true; i++; i = SkipTrivia(src, i); continue; }
            }
            i++;
        }
        return true; // EOF terminates the declaration (ASI)
    }

    // ── shared readers ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads a <c>{ a, b as c }</c> list into (name, alias) pairs (alias == name when no <c>as</c>). A
    /// member name may be an identifier or a string literal (<c>{ "a-b" as c }</c>). A trailing comma is
    /// allowed. On entry <paramref name="i"/> is at the <c>{</c>; on success it is left at the <c>}</c>.
    /// </summary>
    private static bool ReadNamedList(string src, ref int i, List<(string, string)> pairs)
    {
        int n = src.Length;
        if (i >= n || src[i] != '{') return false;
        i = SkipTrivia(src, i + 1);
        while (i < n && src[i] != '}')
        {
            string name;
            if (src[i] is '"' or '\'')
            {
                if (!ReadStringLiteral(src, i, out name, out int afterStr)) return false;
                i = SkipTrivia(src, afterStr);
            }
            else if (IsIdentStart(src[i]))
            {
                name = ReadIdent(src, ref i);
                i = SkipTrivia(src, i);
            }
            else
            {
                return false;
            }

            string alias = name;
            if (StartsWord(src, i, "as"))
            {
                i = SkipTrivia(src, i + 2);
                if (i < n && (src[i] is '"' or '\''))
                {
                    if (!ReadStringLiteral(src, i, out alias, out int afterAlias)) return false;
                    i = SkipTrivia(src, afterAlias);
                }
                else if (i < n && IsIdentStart(src[i]))
                {
                    alias = ReadIdent(src, ref i);
                    i = SkipTrivia(src, i);
                }
                else
                {
                    return false;
                }
            }

            pairs.Add((name, alias));
            if (i < n && src[i] == ',') { i = SkipTrivia(src, i + 1); }
            else break;
        }
        if (i >= n || src[i] != '}') return false;
        i++; // consume the closing '}'
        return true;
    }

    // ── low-level lexing helpers ──────────────────────────────────────────────

    internal static int SkipTrivia(string src, int i)
    {
        int n = src.Length;
        while (i < n)
        {
            char c = src[i];
            if (c is ' ' or '\t' or '\r' or '\n' or '\f' or '\v') { i++; continue; }
            if (c == '/' && i + 1 < n && src[i + 1] == '/') { i += 2; while (i < n && src[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < n && src[i + 1] == '*') { i += 2; while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++; i = Math.Min(n, i + 2); continue; }
            break;
        }
        return i;
    }

    internal static int SkipString(string src, int i, char quote)
    {
        int n = src.Length;
        i++;
        while (i < n)
        {
            char c = src[i];
            if (c == '\\') { i += 2; continue; }
            if (c == quote) return i + 1;
            i++;
        }
        return n;
    }

    internal static int SkipTemplate(string src, int i)
    {
        int n = src.Length;
        i++; // past opening `
        while (i < n)
        {
            char c = src[i];
            if (c == '\\') { i += 2; continue; }
            if (c == '`') return i + 1;
            if (c == '$' && i + 1 < n && src[i + 1] == '{')
            {
                i += 2;
                int braces = 1;
                while (i < n && braces > 0)
                {
                    char d = src[i];
                    if (d == '\\') { i += 2; continue; }
                    if (d is '"' or '\'') { i = SkipString(src, i, d); continue; }
                    if (d == '`') { i = SkipTemplate(src, i); continue; }
                    if (d == '{') braces++;
                    else if (d == '}') braces--;
                    i++;
                }
                continue;
            }
            i++;
        }
        return n;
    }

    internal static int SkipRegex(string src, int i)
    {
        int n = src.Length;
        i++; // past opening /
        bool inClass = false;
        while (i < n)
        {
            char c = src[i];
            if (c == '\\') { i += 2; continue; }
            if (c == '\n') return i; // unterminated — bail
            if (c == '[') inClass = true;
            else if (c == ']') inClass = false;
            else if (c == '/' && !inClass) { i++; break; }
            i++;
        }
        // flags
        while (i < n && IsIdentPart(src[i])) i++;
        return i;
    }

    internal static bool RegexAllowed(char prev)
    {
        // A regex may follow nothing, an operator, or an opener — but not an identifier/number/closer/string.
        if (prev == '\0') return true;
        if (IsIdentPart(prev)) return false;
        if (prev is ')' or ']' or '}') return false;
        if (prev is '"' or '\'' or '`') return false;
        return true;
    }

    private static bool ReadStringLiteral(string src, int i, out string value, out int after)
    {
        value = "";
        after = i;
        int n = src.Length;
        if (i >= n || src[i] is not ('"' or '\'')) return false;
        char q = src[i];
        int start = i + 1;
        int j = start;
        var sb = new System.Text.StringBuilder();
        while (j < n)
        {
            char c = src[j];
            if (c == '\\')
            {
                if (j + 1 < n) { sb.Append(src[j + 1]); j += 2; continue; }
                return false;
            }
            if (c == q) { value = sb.ToString(); after = j + 1; return true; }
            sb.Append(c);
            j++;
        }
        return false;
    }

    private static bool ExpectWord(string src, ref int i, string word)
    {
        i = SkipTrivia(src, i);
        if (!StartsWord(src, i, word)) return false;
        i = SkipTrivia(src, i + word.Length);
        return true;
    }

    private static bool StartsWord(string src, int i, string word)
    {
        if (i + word.Length > src.Length) return false;
        for (int k = 0; k < word.Length; k++)
            if (src[i + k] != word[k]) return false;
        int end = i + word.Length;
        if (end < src.Length && IsIdentPart(src[end])) return false;
        if (i > 0 && IsIdentPart(src[i - 1])) return false;
        return true;
    }

    private static string? IsWordAt(string src, int i, out int end)
    {
        end = i;
        if (i >= src.Length || !IsIdentStart(src[i])) return null;
        int j = i;
        while (j < src.Length && IsIdentPart(src[j])) j++;
        end = j;
        return src[i..j];
    }

    private static string ReadIdent(string src, ref int i)
    {
        int start = i;
        while (i < src.Length && IsIdentPart(src[i])) i++;
        return src[start..i];
    }

    /// <summary>Consumes an optional trailing <c>;</c> after a statement, returning the position after it.</summary>
    private static int ConsumeToStatementEnd(string src, int i)
    {
        i = SkipTrivia(src, i);
        if (i < src.Length && src[i] == ';') return i + 1;
        return i;
    }

    internal static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';
    internal static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';
}
