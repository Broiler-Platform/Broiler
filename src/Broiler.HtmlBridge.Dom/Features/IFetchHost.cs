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

    /// <summary>
    /// A real <c>Blob</c> over <paramref name="bytes"/>, for <c>response.blob()</c>. The interface
    /// belongs to <c>BlobBinding</c>, not here — this seam exists so the fetch path hands back the
    /// same object a page's own <c>new Blob(...)</c> produces rather than a look-alike.
    /// </summary>
    Broiler.JavaScript.Runtime.JSValue CreateBlob(byte[] bytes, string contentType);

    /// <summary>
    /// The entry list of a <c>&lt;form&gt;</c> wrapper (HTML §4.10.21.4), or <see langword="null"/>
    /// when the object is not one. <c>new FormData(form)</c> is the shape a page collects a form's
    /// values with, and enumerating the wrapper's own properties — which is what it did — produced
    /// the element's members rather than the form's fields.
    /// </summary>
    IReadOnlyList<KeyValuePair<string, string>>? FormEntriesFor(Broiler.JavaScript.Runtime.JSObject candidate);
}
