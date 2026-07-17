using Broiler.Layout;
using System.Drawing;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    // RF-BRIDGE-1b: when true, element-geometry queries (offset*/client*/
    // getBoundingClientRect/check-layout) resolve through the renderer's real layout
    // engine via the injected ILayoutView instead of the coarse LayoutMetrics
    // estimators. Enabled once increments 1-3 landed (the LayoutMetrics entry points and
    // the anchor resolver route through the provider, and the Broiler.HTML inline-box
    // geometry fix is live on CI) and the increment-4 parity gate confirmed the shared
    // path matches or improves on the estimators — see
    // SharedLayoutGeometryParityTests.Shared_Geometry_Matches_Or_Beats_Estimator_On_CheckLayout_Corpus.
    // Mirrors the LayoutGeometryCacheEnabled test seam.
    internal static bool UseSharedLayoutGeometry = true;

    // RF-BRIDGE-1b increment 6 cutover — the geometry entry points now answer *exclusively*
    // from the shared snapshot: an element with a shared box reads its real geometry and any
    // snapshot-missing element (detached, display:none/contents, text/comment, or an
    // unmaterialised/cross-origin frame the provider cannot lay out) reports zero. The coarse
    // LayoutMetrics estimators that this flag used to gate a fallback to are deleted, so the
    // flag is now vestigial (retained only because the cutover tests still reference it) and
    // toggling it no longer changes behaviour.
    // See docs/roadmap/htmlbridge-blocked-items-completion-roadmap.md milestone 2.4.
    internal static bool UseSharedGeometryExclusively = true;

    // Phase 1 (project-graph repair): the concrete renderer-backed layout view
    // (Broiler.HTML.Headless.HeadlessLayoutView) is injected here so this binding project
    // depends only on the neutral ILayoutView contract in Broiler.Layout — not on
    // Broiler.HTML.Image. A composition root that references the renderer registers the
    // factory once at startup (see the [ModuleInitializer]s in Broiler.Cli / Broiler.Wpt /
    // the test hosts). A bare `new DomBridge()` with no factory set falls back to an empty
    // view, so it never throws and never pulls the renderer stack. This process-static seam
    // is a deliberate, temporary Phase-1 compromise (the roadmap otherwise avoids service
    // locators); Phase 2's BrowserDocumentSession replaces it with constructor injection.
    internal static Func<ILayoutView>? LayoutViewFactory;

    private ILayoutView? _layoutView;

    private ILayoutView LayoutView =>
        _layoutView ??= LayoutViewFactory?.Invoke() ?? NullLayoutView.Instance;

    // Document-scoped teardown: dispose the current view (releasing the renderer's headless
    // container) and drop the per-pass snapshot so a re-attached/re-parsed document lays out
    // fresh. Called from ParseHtml.
    private void DisposeLayoutView()
    {
        _layoutView?.Dispose();
        _layoutView = null;
        _sharedGeometrySnapshot = null;
    }

    // The geometry snapshot for the current WithLayoutGeometryCache read pass. Built
    // lazily on the first shared query and torn down with the pass, so the renderer
    // lays out at most once per pass (one GetRenderDocument call) rather than per
    // element — see ClearSharedGeometrySnapshot in WithLayoutGeometryCache.
    private IReadOnlyDictionary<DomElement, BoxGeometry> _sharedGeometrySnapshot;

    /// <summary>
    /// Looks up real-layout box geometry for <paramref name="element"/> via the injected
    /// <see cref="ILayoutView"/> (RF-BRIDGE-1b), from the current pass's snapshot (built once
    /// per pass). Returns <c>false</c> when the element produced no box (detached /
    /// <c>display:none</c>), so the caller falls back to the estimator. Active only when
    /// <see cref="UseSharedLayoutGeometry"/> is set; the live entry points gate on that.
    /// </summary>
    private bool TryGetSharedLayoutGeometry(DomElement element, out BoxGeometry geometry)
    {
        var snapshot = _sharedGeometrySnapshot ??= BuildSharedGeometrySnapshot();
        return snapshot.TryGetValue(element, out geometry);
    }

    private static readonly IReadOnlyDictionary<DomElement, BoxGeometry> EmptySharedGeometry =
        new Dictionary<DomElement, BoxGeometry>();

    private IReadOnlyDictionary<DomElement, BoxGeometry> BuildSharedGeometrySnapshot()
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

            // Native visual-viewport (Phase 5 endgame, blocker (b)): hand the document-root
            // pinch-zoom scale to the geometry extraction (CollectLayoutGeometry scales the
            // BoxGeometry rects by it — patch 0006) via the thread-static channel, so the snapshot
            // carries the pinch scale natively instead of the DOM `zoom` bake. Thread-static
            // save/restore keeps concurrent layouts unaffected. Off the native path (default) the
            // channel is left at 0 (no scale) and the WPT-runner zoom bake path is untouched.
            var previousVisualViewportScale = Broiler.Layout.Engine.NativeAnchorPlacement.VisualViewportScale;
            Broiler.Layout.Engine.NativeAnchorPlacement.VisualViewportScale =
                NativeVisualViewport && HasActiveVisualViewport() ? GetVisualViewportScale() : 0.0;
            try
            {
                // P4.4b: a materialised iframe/object sub-document is no longer an in-tree
                // #subdoc-root child — hand the layout view the resolver so it projects each
                // referenced content document as a sub-viewport and composes its geometry.
                return LayoutView.GetGeometry(document, viewport, _pageUrl, ResolveContentDocumentForRender);
            }
            finally
            {
                Broiler.Layout.Engine.NativeAnchorPlacement.VisualViewportScale = previousVisualViewportScale;
            }
        }
        catch
        {
            // Any failure building the shared snapshot (reflection, headless layout,
            // etc.) degrades the whole pass to the estimator — a geometry query must
            // never throw because the renderer choked on the document. This is the
            // bridge's safety net; the injected ILayoutView itself no longer swallows
            // the underlying cause.
            return EmptySharedGeometry;
        }
        finally
        {
            RevertZoomSerialization();
        }
    }

    private void ClearSharedGeometrySnapshot() => _sharedGeometrySnapshot = null;

    // Fallback used when no composition root registered a real layout view: geometry queries
    // resolve to an empty map (the same degraded behaviour as a renderer layout failure), so
    // a bridge constructed without the renderer never throws and never references it.
    private sealed class NullLayoutView : ILayoutView
    {
        public static readonly NullLayoutView Instance = new();
        public IReadOnlyDictionary<DomElement, BoxGeometry> GetGeometry(
            DomDocument document, SizeF viewport, string baseUrl,
            Func<DomElement, DomDocument?>? contentDocumentResolver = null) => EmptySharedGeometry;
        public void Dispose() { }
    }
}
