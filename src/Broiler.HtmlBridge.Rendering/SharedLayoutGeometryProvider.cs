using Broiler.Layout;
using System.Collections.Generic;
using System.Drawing;
using Broiler.HTML.Image;
using Broiler.HTML.Orchestration;
using BDom = Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// RF-BRIDGE-1b: drives the renderer's real layout engine headlessly to answer the
/// bridge's element-geometry queries, replacing the coarse <c>LayoutMetrics</c>
/// estimators. The canonical document is laid out once per
/// (document, version, viewport) snapshot and the per-element
/// <see cref="BoxGeometry"/> map is cached; layout re-runs only when the document
/// version or the viewport changes.
/// </summary>
/// <remarks>
/// Lives in <c>HtmlBridge.Rendering</c> (the bridge project that references
/// <c>Broiler.HTML.Image</c>) so <see cref="DomBridge"/> in <c>HtmlBridge.Dom</c> can
/// consume it without taking a direct dependency on <see cref="HtmlContainer"/>.
/// </remarks>
public sealed class SharedLayoutGeometryProvider
{
    private readonly HtmlContainer _container = new()
    {
        AvoidAsyncImagesLoading = true,
        AvoidImagesLateLoading = true,
    };

    private static readonly IReadOnlyDictionary<BDom.DomElement, BoxGeometry> EmptyGeometry =
        new Dictionary<BDom.DomElement, BoxGeometry>();

    private IReadOnlyDictionary<BDom.DomElement, BoxGeometry> _snapshot;
    private BDom.DomDocument _snapshotDocument;
    private ulong _snapshotVersion;
    private SizeF _snapshotViewport;
    private bool _hasSnapshot;

    /// <summary>
    /// Returns the per-element geometry map for <paramref name="document"/> at its
    /// current <see cref="DomDocument.Version"/> and the given
    /// <paramref name="viewport"/>, laying out only when the cached snapshot is stale.
    /// </summary>
    public IReadOnlyDictionary<BDom.DomElement, BoxGeometry> GetGeometry(
        BDom.DomDocument document, SizeF viewport, string baseUrl)
    {
        if (_hasSnapshot
            && ReferenceEquals(_snapshotDocument, document)
            && _snapshotVersion == document.Version
            && _snapshotViewport == viewport)
        {
            return _snapshot;
        }

        IReadOnlyDictionary<BDom.DomElement, BoxGeometry> snapshot;
        try
        {
            _container.SetDocumentWithStyleSet(document, baseUrl: baseUrl);
            snapshot = _container.GetLayoutGeometry(viewport);
        }
        catch
        {
            // A layout failure must never break a script geometry query. Degrade to an
            // empty map so callers fall back to the estimator; cache it for this
            // (document, version, viewport) so the failing layout is not retried per
            // element within the pass.
            snapshot = EmptyGeometry;
        }

        _snapshot = snapshot;
        _snapshotDocument = document;
        _snapshotVersion = document.Version;
        _snapshotViewport = viewport;
        _hasSnapshot = true;
        return _snapshot;
    }

    /// <summary>
    /// Looks up box geometry for a single <paramref name="element"/> in the current
    /// snapshot. Returns <c>false</c> when the element produced no box (detached or
    /// <c>display:none</c>).
    /// </summary>
    public bool TryGetGeometry(
        BDom.DomDocument document, SizeF viewport, string baseUrl, BDom.DomElement element, out BoxGeometry geometry)
        => GetGeometry(document, viewport, baseUrl).TryGetValue(element, out geometry);
}
