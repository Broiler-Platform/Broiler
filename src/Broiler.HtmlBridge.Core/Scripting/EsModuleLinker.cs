using System;
using System.Collections.Generic;
using System.Text;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// Rewrites one module's source (Phase 7 item 6) into a plain-JavaScript program that a non-module
/// evaluator (<c>JSContext.Eval</c>) can run, wiring its <c>import</c>/<c>export</c> statements to a
/// runtime module registry shared across the document's module graph. Each module becomes a strict IIFE
/// that (1) registers its exports object under its key before running (so a circular import sees the
/// in-progress object), (2) reads its imports from the registry, and (3) publishes its exports.
/// </summary>
/// <remarks>
/// <para>Bindings are <b>snapshots</b> taken at the end of the exporting module's evaluation, not live
/// bindings. Because the graph is evaluated dependency-first, an acyclic import always reads a fully
/// populated exports object; the snapshot only differs from spec behaviour for a value reassigned after
/// evaluation or read across a cycle. A namespace import (<c>import * as ns</c>) binds the exports object
/// itself, so it does reflect later writes.</para>
/// <para>Each <c>import.meta</c> occurrence is rewritten to a synthesized per-module object whose
/// <c>url</c> is the module's registry key. A dynamic <c>import(spec)</c> is routed through the module
/// graph: it resolves to a <c>Promise</c> of the already-linked module namespace (the graph loader
/// resolves/fetches/links a string-literal specifier ahead of time; an unresolved or runtime-computed
/// specifier rejects). Live cross-cycle bindings and top-level <c>await</c> as genuinely async remain
/// engine-coupled and are tracked separately.</para>
/// <para>The bootstrap program (<see cref="Bootstrap"/>) must run once before any module program.</para>
/// </remarks>
internal static class EsModuleLinker
{
    /// <summary>The registry helpers, evaluated once before the module programs.</summary>
    public const string Bootstrap =
        "globalThis.__brmods=globalThis.__brmods||{};" +
        "globalThis.__brreg=function(k){var m=globalThis.__brmods[k];if(!m){m={};globalThis.__brmods[k]=m;}return m;};" +
        "globalThis.__brqr=function(k){var m=globalThis.__brmods[k];if(!m){m={};globalThis.__brmods[k]=m;}return m;};" +
        // Dynamic import(): a per-module specifier→key map (__brdynmap) and a loader that returns a Promise
        // of the already-linked module namespace (or rejects for an unknown/unresolved specifier).
        "globalThis.__brdynmap=globalThis.__brdynmap||{};" +
        "globalThis.__brdynimp=globalThis.__brdynimp||function(b,s){return new Promise(function(res,rej){" +
        "var mp=globalThis.__brdynmap[b];var k=mp&&mp[s];var m=k?globalThis.__brmods[k]:null;" +
        "if(m){res(m);}else{rej(new Error(\"Cannot find module: \"+s));}});};";

    private readonly record struct Edit(int Start, int Length, string Text);

    /// <summary>
    /// Renders <paramref name="source"/> (whose parsed <paramref name="syntax"/> is
    /// <see cref="EsModuleSyntax.Supported"/>) into its linked program. <paramref name="keyOf"/> maps an
    /// import specifier to its resolved registry key (the resolved module URL); it must return non-null for
    /// every dependency (the graph loader guarantees this before rendering).
    /// </summary>
    public static string Render(string source, EsModuleSyntax syntax, string moduleKey, Func<string, string?> keyOf)
    {
        var edits = new List<Edit>();
        var endAssigns = new StringBuilder();

        foreach (var imp in syntax.Imports)
        {
            var key = keyOf(imp.Specifier);
            var sb = new StringBuilder();
            if (key == null)
            {
                // Unresolved dependency — fail loudly at runtime rather than silently binding undefined.
                sb.Append("throw new Error(\"Module not found: ").Append(JsEscapeRaw(imp.Specifier)).Append("\");");
            }
            else
            {
                var req = $"globalThis.__brqr({JsString(key)})";
                if (imp.NamespaceName != null)
                    sb.Append("var ").Append(imp.NamespaceName).Append('=').Append(req).Append(';');
                if (imp.DefaultName != null)
                    sb.Append("var ").Append(imp.DefaultName).Append('=').Append(req).Append("[\"default\"];");
                foreach (var (imported, local) in imp.Named)
                    sb.Append("var ").Append(local).Append('=').Append(req).Append('[').Append(JsString(imported)).Append("];");
                if (imp.NamespaceName == null && imp.DefaultName == null && imp.Named.Count == 0)
                    sb.Append(req).Append(';'); // side-effect import
            }
            edits.Add(new Edit(imp.Start, imp.Length, sb.ToString()));
        }

        foreach (var exp in syntax.Exports)
        {
            switch (exp.Kind)
            {
                case ModuleExportKind.Declaration:
                    // Strip `export `; publish the declared names at module end (after the declaration runs).
                    edits.Add(new Edit(exp.Start, exp.Length, string.Empty));
                    foreach (var name in exp.Names!)
                        endAssigns.Append("__E[").Append(JsString(name)).Append("]=").Append(name).Append(';');
                    break;

                case ModuleExportKind.NamedList:
                    // Remove the statement; publish local→exported at module end (locals may be hoisted).
                    edits.Add(new Edit(exp.Start, exp.Length, string.Empty));
                    foreach (var (local, exported) in exp.Bindings!)
                        endAssigns.Append("__E[").Append(JsString(exported)).Append("]=").Append(local).Append(';');
                    break;

                case ModuleExportKind.ReExportNamed:
                {
                    var key = keyOf(exp.Specifier!);
                    var sb = new StringBuilder();
                    if (key == null)
                        sb.Append("throw new Error(\"Module not found: ").Append(JsEscapeRaw(exp.Specifier!)).Append("\");");
                    else
                    {
                        var req = $"globalThis.__brqr({JsString(key)})";
                        foreach (var (imported, exported) in exp.Bindings!)
                            sb.Append("__E[").Append(JsString(exported)).Append("]=").Append(req).Append('[').Append(JsString(imported)).Append("];");
                    }
                    edits.Add(new Edit(exp.Start, exp.Length, sb.ToString()));
                    break;
                }

                case ModuleExportKind.ReExportStar:
                {
                    var key = keyOf(exp.Specifier!);
                    string text = key == null
                        ? $"throw new Error(\"Module not found: {JsEscapeRaw(exp.Specifier!)}\");"
                        : $"(function(_s){{for(var _k in _s){{if(_k!==\"default\")__E[_k]=_s[_k];}}}})(globalThis.__brqr({JsString(key)}));";
                    edits.Add(new Edit(exp.Start, exp.Length, text));
                    break;
                }

                case ModuleExportKind.ReExportStarAs:
                {
                    var key = keyOf(exp.Specifier!);
                    string text = key == null
                        ? $"throw new Error(\"Module not found: {JsEscapeRaw(exp.Specifier!)}\");"
                        : $"__E[{JsString(exp.NamespaceName!)}]=globalThis.__brqr({JsString(key)});";
                    edits.Add(new Edit(exp.Start, exp.Length, text));
                    break;
                }

                case ModuleExportKind.Default:
                    // Replace `export ... default` with an assignment; the expression/decl that follows stays.
                    edits.Add(new Edit(exp.Start, exp.Length, "__E[\"default\"]="));
                    break;
            }
        }

        // import.meta → a synthesized per-module meta object (see the preamble below).
        foreach (var (start, length) in syntax.ImportMetaSpans)
            edits.Add(new Edit(start, length, "__brmeta"));

        // Dynamic import(spec) → a per-module graph-backed loader (see the preamble below).
        foreach (var d in syntax.DynamicImports)
            edits.Add(new Edit(d.KeywordStart, d.KeywordLength, "__brdimp"));

        edits.Sort((a, b) => a.Start.CompareTo(b.Start));

        var body = new StringBuilder(source.Length + 64);
        int pos = 0;
        foreach (var e in edits)
        {
            if (e.Start > pos)
                body.Append(source, pos, e.Start - pos);
            body.Append(e.Text);
            pos = e.Start + e.Length;
        }
        if (pos < source.Length)
            body.Append(source, pos, source.Length - pos);

        var program = new StringBuilder();
        // Self-bootstrap: the registry helpers are idempotent, so every module program can define them and
        // the programs can run in any dependency-first order without a separate bootstrap step.
        program.Append(Bootstrap).Append('\n');
        program.Append("(function(){\"use strict\";\n");
        program.Append("var __E=globalThis.__brreg(").Append(JsString(moduleKey)).Append(");\n");
        // import.meta: a fresh object per module. `url` is the module's registry key (its resolved URL, or
        // the synthetic id for an inline module). Declared only when the module references import.meta.
        if (syntax.ImportMetaSpans.Count > 0)
            program.Append("var __brmeta={url:").Append(JsString(moduleKey)).Append("};\n");
        // Dynamic import(): bind this module's key into the loader and publish its resolved specifier→key
        // map, so a top-level import() sees it before the body runs.
        if (syntax.DynamicImports.Count > 0)
        {
            var keyLit = JsString(moduleKey);
            program.Append("var __brdimp=function(__s){return globalThis.__brdynimp(").Append(keyLit).Append(",__s);};\n");
            program.Append("globalThis.__brdynmap[").Append(keyLit).Append("]=globalThis.__brdynmap[").Append(keyLit).Append("]||{};\n");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in syntax.DynamicImports)
            {
                if (string.IsNullOrEmpty(d.LiteralSpecifier) || !seen.Add(d.LiteralSpecifier!)) continue;
                var depKey = keyOf(d.LiteralSpecifier!);
                if (depKey == null) continue; // unresolved — the loader rejects it at runtime
                program.Append("globalThis.__brdynmap[").Append(keyLit).Append("][")
                    .Append(JsString(d.LiteralSpecifier!)).Append("]=").Append(JsString(depKey)).Append(";\n");
            }
        }
        program.Append(body);
        program.Append('\n');
        program.Append(endAssigns);
        program.Append("\n})();");
        return program.ToString();
    }

    /// <summary>Produces a JS double-quoted string literal for <paramref name="s"/>.</summary>
    private static string JsString(string s) => "\"" + JsEscapeRaw(s) + "\"";

    private static string JsEscapeRaw(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\u2028': sb.Append("\\u2028"); break;
                case '\u2029': sb.Append("\\u2029"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
