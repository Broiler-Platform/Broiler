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
/// <para>Bindings are <b>live</b>: each export is published as a getter on the exports object that reads the
/// current value of the exported local, so a value reassigned after the module finishes (e.g. a counter
/// mutated by an exported function) is observed by importers. A namespace import (<c>import * as ns</c>)
/// reads those getters member-wise; a named import (<c>import { x }</c>) is made live by rewriting its reads
/// to the same getter access (<see cref="EsModuleLiveRefs"/>), conservatively — a module the rewriter cannot
/// prove safe keeps a correct <em>snapshot</em> binding instead. A default import is a value, not a live
/// binding. Because the graph is evaluated dependency-first, an acyclic import already reads a fully
/// populated exports object.</para>
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
        var liveExports = new StringBuilder();
        // Named-import local name → its live read expression (e.g. __src0["imported"]); used by the live-ref
        // rewriter. Spans to leave untouched by that rewriter (binding declarations, not reads).
        var namedLocals = new Dictionary<string, string>(StringComparer.Ordinal);
        var skip = new List<(int Start, int End)>();
        int srcCounter = 0;

        foreach (var imp in syntax.Imports)
        {
            skip.Add((imp.Start, imp.Start + imp.Length));
            var key = keyOf(imp.Specifier);
            var sb = new StringBuilder();
            if (key == null)
            {
                // Unresolved dependency — fail loudly at runtime rather than silently binding undefined.
                sb.Append("throw new Error(\"Module not found: ").Append(JsEscapeRaw(imp.Specifier)).Append("\");");
            }
            else
            {
                // The source module's registry (exports) object. Named-import reads go through it live
                // (against the exporter's live getters); the `var` snapshot bindings below are a safety net
                // for any read the conservative live-ref rewriter cannot prove safe (correct, just not live).
                var srcVar = "__src" + srcCounter++;
                sb.Append("var ").Append(srcVar).Append("=globalThis.__brqr(").Append(JsString(key)).Append(");");
                if (imp.NamespaceName != null)
                    sb.Append("var ").Append(imp.NamespaceName).Append('=').Append(srcVar).Append(';');
                if (imp.DefaultName != null)
                    sb.Append("var ").Append(imp.DefaultName).Append('=').Append(srcVar).Append("[\"default\"];");
                foreach (var (imported, local) in imp.Named)
                {
                    sb.Append("var ").Append(local).Append('=').Append(srcVar).Append('[').Append(JsString(imported)).Append("];");
                    namedLocals[local] = $"{srcVar}[{JsString(imported)}]";
                }
                // A side-effect import needs no binding; the __brqr above already links the module.
            }
            edits.Add(new Edit(imp.Start, imp.Length, sb.ToString()));
        }

        foreach (var exp in syntax.Exports)
        {
            switch (exp.Kind)
            {
                case ModuleExportKind.Declaration:
                    // Strip `export `; publish each declared name as a live getter (reflects later reassignment).
                    edits.Add(new Edit(exp.Start, exp.Length, string.Empty));
                    foreach (var name in exp.Names!)
                        AppendLiveExport(liveExports, name, name);
                    break;

                case ModuleExportKind.NamedList:
                    // Remove the statement; publish local→exported as live getters (locals may be hoisted).
                    skip.Add((exp.Start, exp.Start + exp.Length));
                    edits.Add(new Edit(exp.Start, exp.Length, string.Empty));
                    foreach (var (local, exported) in exp.Bindings!)
                        AppendLiveExport(liveExports, exported, local);
                    break;

                case ModuleExportKind.ReExportNamed:
                {
                    skip.Add((exp.Start, exp.Start + exp.Length));
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
                    skip.Add((exp.Start, exp.Start + exp.Length));
                    var key = keyOf(exp.Specifier!);
                    string text = key == null
                        ? $"throw new Error(\"Module not found: {JsEscapeRaw(exp.Specifier!)}\");"
                        : $"(function(_s){{for(var _k in _s){{if(_k!==\"default\")__E[_k]=_s[_k];}}}})(globalThis.__brqr({JsString(key)}));";
                    edits.Add(new Edit(exp.Start, exp.Length, text));
                    break;
                }

                case ModuleExportKind.ReExportStarAs:
                {
                    skip.Add((exp.Start, exp.Start + exp.Length));
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

        // Make named-import reads live where provably safe (else the snapshot bindings above stand).
        var refEdits = EsModuleLiveRefs.TryBuild(source, namedLocals, skip);
        if (refEdits != null)
            foreach (var re in refEdits)
                edits.Add(new Edit(re.Start, re.Length, re.Text));

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
        // Live export accessors: getters that read the current value of each exported local, so a value
        // reassigned after the module finishes (or across a cycle) is observed by importers. Defined before
        // the body so a cyclic namespace read sees them (TDZ applies to a not-yet-initialised let/const,
        // which is spec-correct). Namespace imports read these directly; named imports read via the live-ref
        // rewriter (EsModuleLiveRefs).
        if (liveExports.Length > 0)
            program.Append(liveExports).Append('\n');
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
        program.Append("\n})();");
        return program.ToString();
    }

    /// <summary>Appends a live-binding export: a getter on the exports object that returns the current value
    /// of the exported local, so importers observe reassignments after the module has finished.</summary>
    private static void AppendLiveExport(StringBuilder sb, string exported, string local) =>
        sb.Append("Object.defineProperty(__E,").Append(JsString(exported))
          .Append(",{get:function(){return ").Append(local).Append(";},enumerable:true,configurable:true});");

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
