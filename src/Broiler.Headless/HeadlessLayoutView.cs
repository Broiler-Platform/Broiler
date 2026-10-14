using System.Drawing;
using Broiler.HTML.Image;
using Broiler.Layout;
using BDom = Broiler.Dom;

namespace Broiler.Headless;

/// <summary>
/// <see cref="ILayoutView"/> implementation that drives the renderer's real layout engine
/// headlessly (no paint backend) to answer the script bridge's element-geometry queries,
/// replacing the coarse estimators.
/// </summary>
/// <remarks>
/// <para>
/// Broiler.Layout owns the contract and ships no implementation of it, and the bridge depends
/// only on the contract; each host here injects this view through
/// <c>DomBridge.LayoutViewFactory</c> (see its <c>HeadlessLayoutViewRegistration</c>).
/// </para>
/// <para>
/// The renderer places anchor-positioned boxes itself (<see cref="HtmlContainer.PlacesAnchoredBoxes"/>),
/// so the geometry scripts read during <c>offsetLeft</c> or <c>getBoundingClientRect()</c> carries
/// engine-resolved <c>position-area</c>, <c>anchor()</c>, <c>anchor-size()</c> and
/// <c>@position-try</c> placement instead of the boxes' static position.
/// </para>
/// <para>
/// It keeps no geometry cache of its own. The bridge already caches the snapshot, keyed on
/// everything that changes it — document and sub-document versions, viewport, native zoom and
/// the visual-viewport scale — and hands this view a fresh render projection with a
/// content-document resolver on every build, which a per-view cache could never have matched.
/// </para>
/// </remarks>
public sealed class HeadlessLayoutView : ILayoutView
{
    private readonly HtmlContainer _container = new()
    {
        AvoidAsyncImagesLoading = true,
        AvoidImagesLateLoading = true,
        PlacesAnchoredBoxes = true,
    };

    private bool _disposed;

    /// <summary>
    /// Lays <paramref name="document"/> out at <paramref name="viewport"/> and returns the
    /// per-element geometry map, keyed by the canonical <see cref="BDom.DomElement"/>.
    /// </summary>
    public IReadOnlyDictionary<BDom.DomElement, BoxGeometry> GetGeometry(
        BDom.DomDocument document, SizeF viewport, string baseUrl,
        Func<BDom.DomElement, BDom.DomDocument?>? contentDocumentResolver = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // A layout fault is not swallowed here: it propagates to the caller (the bridge's
        // BuildSharedGeometrySnapshot degrades the whole pass to an empty map), so the cause
        // is not hidden behind a per-provider catch-all.
        _container.ContentDocumentResolver = contentDocumentResolver;
        _container.SetDocumentWithStyleSet(document, baseUrl: baseUrl);
        return _container.GetLayoutGeometry(viewport);
    }

    /// <summary>Releases the internal renderer container.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _container.Dispose();
    }
}
