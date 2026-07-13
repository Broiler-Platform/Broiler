namespace Broiler.HtmlBridge.Dom.Features;

/// <summary>
/// The narrow bridge service the <see cref="FetchBinding"/> feature module needs (HtmlBridge
/// complexity-reduction roadmap Phase 3, P3.11). Networking is otherwise self-contained — host I/O
/// goes through the injected <see cref="Broiler.HtmlBridge.Dom.Runtime.ResourceLoader"/> — so the only
/// bridge coupling is the current page URL, used as the base when resolving a relative
/// <c>Response.redirect</c> target. Exposed as a named member implemented explicitly on
/// <see cref="DomBridge"/> so the public surface is unchanged.
/// </summary>
internal interface IFetchHost
{
    /// <summary>The document's current URL, used as the base for resolving relative redirect URLs.</summary>
    string PageUrl { get; }
}
