namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// Wraps an inline <c>&lt;script type="module"&gt;</c> body so a plain-script evaluator runs it with the
/// module top-level semantics that matter for an import-free module (Phase 7 item 6, first slice): module
/// code is always strict, and its top-level declarations live in the module scope rather than leaking to
/// the global object. A strict immediately-invoked function expression reproduces both: <c>"use strict"</c>
/// applies, top-level <c>var</c>/<c>let</c>/<c>const</c>/<c>function</c> stay local, and the top-level
/// <c>this</c> is <c>undefined</c> (a strict function invoked without a receiver) as in a module.
/// </summary>
/// <remarks>
/// This is deliberately a first slice: it does not provide <c>import</c>/<c>export</c>, <c>import.meta</c>
/// or top-level <c>await</c>. A wrapped module that uses those raises a syntax/runtime error at execution
/// (surfaced to the caller's logger) rather than being silently skipped — full module-graph loading is a
/// later slice.
/// </remarks>
public static class ModuleScriptWrapper
{
    /// <summary>Returns <paramref name="body"/> wrapped as a strict, self-scoped module IIFE.</summary>
    public static string WrapInlineModule(string body) =>
        "(function () { \"use strict\";\n" + body + "\n})();";
}
