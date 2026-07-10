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

    // RF-BRIDGE-1b increment 6 cutover — ON by default (2026-07-10). When true, the
    // geometry entry points answer *exclusively* from the shared snapshot: an element that
    // genuinely generates no box (display:none/contents, text/comment — see
    // HasAssociatedLayoutBox) reports zero geometry instead of consulting the coarse
    // LayoutMetrics estimators. A box-generating element that is merely absent from the
    // current snapshot still falls back to the estimator (see ShouldReturnExclusiveSharedZero)
    // — the snapshot can transiently miss a laid-out element (observed in the WPT test
    // harness's ExecuteScriptsWithDom flow, NOT in normal Attach+scrollIntoView usage; see
    // milestone 2.4), and zeroing it would mis-collapse its geometry. That refinement is what
    // makes this flippable without regressing the zoom scroll-into-view WPT pixel tests. Net
    // behaviour change vs flag-off: only display:none/contents elements switch from an
    // estimator guess to a correct zero.
    // See docs/roadmap/htmlbridge-blocked-items-completion-roadmap.md milestone 2.4.
    internal static bool UseSharedGeometryExclusively = true;

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
