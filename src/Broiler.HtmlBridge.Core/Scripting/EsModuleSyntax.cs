using System;
using System.Collections.Generic;
using System.Text;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// A single top-level <c>import</c> statement parsed from a module (Phase 7 item 6). Models the union of
/// the import forms: a default binding, a namespace (<c>* as ns</c>) binding, and/or a named list, plus a
/// side-effect-only import (all binding fields empty). <see cref="Start"/>/<see cref="Length"/> mark the
/// statement's span in the module source so the linker can replace it in place.
/// </summary>
internal sealed record ModuleImport(
    string Specifier,
    string? DefaultName,
    string? NamespaceName,
    IReadOnlyList<(string Imported, string Local)> Named,
    int Start,
    int Length);

/// <summary>The kind of a parsed top-level <c>export</c> statement (Phase 7 item 6).</summary>
internal enum ModuleExportKind
{
    /// <summary><c>export const/let/var/function/class NAME …</c> — the declaration stays; NAME is exported.</summary>
    Declaration,

    /// <summary><c>export { a, b as c }</c> — local bindings exported (optionally renamed).</summary>
    NamedList,

    /// <summary><c>export { a, b as c } from "spec"</c> — re-export named bindings from another module.</summary>
    ReExportNamed,

    /// <summary><c>export * from "spec"</c> — re-export all (except <c>default</c>).</summary>
    ReExportStar,

    /// <summary><c>export * as ns from "spec"</c> — re-export the namespace under a name.</summary>
    ReExportStarAs,

    /// <summary><c>export default EXPR</c> / <c>export default function/class …</c>.</summary>
    Default,
}

/// <summary>
/// A single top-level <c>export</c> statement parsed from a module (Phase 7 item 6). The fields used
/// depend on <see cref="Kind"/>; <see cref="Start"/>/<see cref="Length"/> mark the span the linker rewrites.
/// </summary>
internal sealed record ModuleExport(
    ModuleExportKind Kind,
    int Start,
    int Length,
    string? Specifier = null,
    IReadOnlyList<(string Local, string Exported)>? Bindings = null,
    IReadOnlyList<string>? Names = null,
    string? NamespaceName = null,
    string? DefaultBoundName = null);

/// <summary>
/// The parsed module-syntax view of one module source (Phase 7 item 6): its top-level static
/// <c>import</c>/<c>export</c> statements. <see cref="Supported"/> is <c>false</c> when the scanner meets a
/// form it does not confidently handle (destructuring exports, an unterminated statement, etc.) — the
/// linker then falls back to running the module as-is rather than emitting a wrong transform, so the
/// feature is strictly additive.
/// </summary>
internal sealed class EsModuleSyntax
{
    /// <summary>Top-level <c>import</c> statements in source order.</summary>
    public List<ModuleImport> Imports { get; } = [];

    /// <summary>Top-level <c>export</c> statements in source order.</summary>
    public List<ModuleExport> Exports { get; } = [];

    /// <summary>Whether every top-level import/export was recognised (so the transform is safe to apply).</summary>
    public bool Supported { get; internal set; } = true;

    /// <summary>Whether the module has any static import/export at all (else it needs no linking).</summary>
    public bool HasModuleSyntax => Imports.Count > 0 || Exports.Count > 0;

    /// <summary>The distinct import specifiers this module depends on, in first-seen order (imports then re-exports).</summary>
    public IReadOnlyList<string> Dependencies()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deps = new List<string>();
        void Add(string? s)
        {
            if (!string.IsNullOrEmpty(s) && seen.Add(s)) deps.Add(s);
        }
        foreach (var imp in Imports) Add(imp.Specifier);
        foreach (var exp in Exports) Add(exp.Specifier);
        return deps;
    }
}
