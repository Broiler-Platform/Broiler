using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Logging;
using Broiler.JavaScript.Engine;

namespace Broiler.Wpt;

/// <summary>
/// Registers each element that has an <c>id</c> as a global of the same name — the runner's
/// approximation of HTML "named access on the Window object" (<c>window.myId</c>).
/// </summary>
/// <remarks>
/// <para>
/// Broiler.HtmlBridge deleted its <c>DomBridge.RegisterNamedElementGlobals</c> in "Delete four DOM
/// couplings rather than port them", finding no caller in that repository; this runner was the
/// caller. The bridge does not implement named access, so the runner keeps the approximation it has
/// always rendered tests with, here, until the bridge implements the real thing against its realm.
/// </para>
/// <para>
/// It is a snapshot taken after the document is attached, not the live named-property object the
/// specification describes: an element added later gets no global, and one removed keeps it.
/// </para>
/// </remarks>
internal static class NamedElementGlobals
{
    internal static void Register(DomBridge bridge, JSContext context)
    {
        foreach (var el in bridge.Elements)
        {
            if (string.IsNullOrEmpty(el.Id))
                continue;

            // Only register if the global doesn't already exist (user-defined globals take
            // precedence — a `var`/`function` or a lexical `const`/`let`/`class` with the same name
            // shadows the named element, per HTML "named access on the Window object").
            try
            {
                var existing = context.Eval($"typeof {el.Id} !== 'undefined'");
                if (existing != null && existing.BooleanValue)
                    continue;
            }
            catch
            {
                // If the id isn't a valid JS identifier, or resolving it throws (e.g. a lexical
                // binding still in its temporal dead zone), leave the existing binding untouched.
                continue;
            }

            // Defensive: even past the guard, assigning could hit a read-only binding in an edge
            // case; a named-element convenience global must never crash script execution.
            try
            {
                // The assignment is against a JSContext indexer, which is Broiler.JS's own, so it
                // takes the engine's object. ToEngineObject and not ToEngineValue -- the latter
                // answers null for a handle carrying a primitive, and would store a null global
                // rather than refuse.
                context[el.Id] = Broiler.HtmlBridge.Dom.Runtime.JsInterop.ToEngineObject(bridge.WrapNode(el));
            }
            catch (Exception ex)
            {
                RenderLogger.LogWarning(LogCategory.JavaScript, "NamedElementGlobals.Register",
                    $"Could not register named element global '{el.Id}': {ex.Message}", ex);
            }
        }
    }
}
