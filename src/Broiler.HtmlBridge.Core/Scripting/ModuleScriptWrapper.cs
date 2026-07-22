namespace Broiler.HtmlBridge.Internal.Scripting;

/// <summary>
/// Wraps an inline <c>&lt;script type="module"&gt;</c> body so a plain-script evaluator runs it with the
/// module top-level semantics that matter for an import-free module (Phase 7 item 6, first slice): module
/// code is always strict, and its top-level declarations live in the module scope rather than leaking to
/// the global object. A strict immediately-invoked function expression reproduces both: <c>"use strict"</c>
/// applies, top-level <c>var</c>/<c>let</c>/<c>const</c>/<c>function</c> stay local, and the top-level
/// <c>this</c> is <c>undefined</c> (a strict function invoked without a receiver) as in a module.
/// </summary>
/// <remarks>
/// This wrapper handles a module with no <c>import</c>/<c>export</c>. Modules that use them are linked by
/// the <see cref="ModuleGraphLoader"/> (resolve + fetch + dedup + dependency-first order + import/export
/// rewrite); this wrapper is the loader's <b>fallback</b> for a module whose syntax the scanner cannot
/// confidently transform (destructuring exports, top-level <c>await</c>) — the module then runs as-is and
/// any unsupported construct surfaces its error at execution rather than being silently skipped.
/// </remarks>
internal static class ModuleScriptWrapper
{
    /// <summary>Returns <paramref name="body"/> wrapped as a strict, self-scoped module IIFE.</summary>
    public static string WrapInlineModule(string body) =>
        "(function () { \"use strict\";\n" + body + "\n})();";
}
