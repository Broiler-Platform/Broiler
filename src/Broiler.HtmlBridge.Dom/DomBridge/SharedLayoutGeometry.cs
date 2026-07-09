using Broiler.Layout;
using System.Drawing;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // RF-BRIDGE-1b: when true, element-geometry queries (offset*/client*/
    // getBoundingClientRect/check-layout) resolve through the renderer's real layout
    // engine via SharedLayoutGeometryProvider instead of the coarse LayoutMetrics
    // estimators. Enabled once increments 1-3 landed (the LayoutMetrics entry points and
    // the anchor resolver route through the provider, and the Broiler.HTML inline-box
    // geometry fix is live on CI) and the increment-4 parity gate confirmed the shared
    // path matches or improves on the estimators — see
    // SharedLayoutGeometryParityTests.Shared_Geometry_Matches_Or_Beats_Estimator_On_CheckLayout_Corpus.
    // Mirrors the LayoutGeometryCacheEnabled test seam.
    internal static bool UseSharedLayoutGeometry = true;

    // RF-BRIDGE-1b increment 6 (staged cutover — default OFF). When true, the geometry
    // entry points answer *exclusively* from the shared snapshot for unzoomed elements:
    // an unzoomed element with no shared box returns zero (detached / display:none
    // semantics) instead of falling back to the coarse LayoutMetrics estimators. This
    // stages the estimator deletion — flipping it on makes the estimator bodies dead
    // for the unzoomed path. It stays OFF until (a) the zoom-correct shared snapshot
    // lands (so zoomed subtrees no longer need the estimator — they are still routed to
    // it by IsUnzoomedForSharedGeometry) and (b) the pre-layout resolvers (sticky/
    // position-area, which depend on scroll-offset accounting the snapshot lacks) have a
    // shared-geometry source. Only then can the estimator bodies actually be removed.
    // See docs/roadmap/rf-bridge-1b-layout-unification.md §5 incr 6 "Deletion sequence".
    internal static bool UseSharedGeometryExclusively = false;

    private SharedLayoutGeometryProvider _sharedLayoutGeometry;

    private SharedLayoutGeometryProvider SharedLayoutGeometry =>
        _sharedLayoutGeometry ??= new SharedLayoutGeometryProvider();

    // The geometry snapshot for the current WithLayoutGeometryCache read pass. Built
    // lazily on the first shared query and torn down with the pass, so the renderer
    // lays out at most once per pass (one GetRenderDocument call) rather than per
    // element — see ClearSharedGeometrySnapshot in WithLayoutGeometryCache.
    private System.Collections.Generic.IReadOnlyDictionary<Broiler.Dom.DomElement, BoxGeometry> _sharedGeometrySnapshot;

    /// <summary>
    /// Looks up real-layout box geometry for <paramref name="element"/> via the renderer
    /// (RF-BRIDGE-1b), from the current pass's snapshot (built once per pass). Returns
    /// <c>false</c> when the element produced no box (detached / <c>display:none</c>), so
    /// the caller falls back to the estimator. Active only when
    /// <see cref="UseSharedLayoutGeometry"/> is set; the live entry points gate on that.
    /// </summary>
    private bool TryGetSharedLayoutGeometry(DomElement element, out BoxGeometry geometry)
    {
        var snapshot = _sharedGeometrySnapshot ??= BuildSharedGeometrySnapshot();
        return snapshot.TryGetValue(element, out geometry);
    }

    private static readonly System.Collections.Generic.IReadOnlyDictionary<Broiler.Dom.DomElement, BoxGeometry> EmptySharedGeometry =
        new System.Collections.Generic.Dictionary<Broiler.Dom.DomElement, BoxGeometry>();

    private System.Collections.Generic.IReadOnlyDictionary<Broiler.Dom.DomElement, BoxGeometry> BuildSharedGeometrySnapshot()
    {
        // RF-BRIDGE-1b render-doc/live-doc separation: install a revert log so the zoom
        // baking GetRenderDocument applies (needed for correct RENDERED geometry, incl. a
        // zoomed descendant's overflow) is undone on the live document afterward. That keeps
        // the live/CSSOM document pristine, so unzoomed CSSOM queries on zoomed elements
        // (routed to the estimator) read original styles instead of baked ones.
        _zoomSerializationRevertLog = [];
        try
        {
            var document = GetRenderDocument();
            var viewport = new SizeF(_viewportWidth, _viewportHeight);
            return SharedLayoutGeometry.GetGeometry(document, viewport, _pageUrl);
        }
        catch
        {
            // Any failure building the shared snapshot (reflection, headless layout,
            // etc.) degrades the whole pass to the estimator — a geometry query must
            // never throw because the renderer choked on the document.
            return EmptySharedGeometry;
        }
        finally
        {
            RevertZoomSerialization();
        }
    }

    private void ClearSharedGeometrySnapshot() => _sharedGeometrySnapshot = null;
}
