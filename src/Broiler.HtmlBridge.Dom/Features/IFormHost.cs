using Broiler.JavaScript.Runtime;
using Broiler.Dom;

namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow bridge service the <see cref="FormBinding"/> feature module needs (HtmlBridge
/// complexity-reduction roadmap Phase 3, P3.9). The HTMLFormElement interface — the form-controls
/// collection (with named access) and validity — is otherwise pure tree/attribute work over the
/// assembly's static <c>DomBridge</c> helpers; the only bridge coupling is turning a resolved form
/// control into its JS wrapper. This replaces the <c>DomBridge</c> back-reference the old
/// <c>FormElementsCollection</c> carried purely for that purpose.
/// </summary>
internal interface IFormHost
{
    /// <summary>Returns the single JS wrapper identity for <paramref name="node"/>.</summary>
    JSObject ToJSObject(DomNode node);
}
