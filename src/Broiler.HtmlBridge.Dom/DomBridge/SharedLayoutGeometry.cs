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
    // See docs/architecture/htmlbridge.md#layout-and-geometry.
    internal static bool UseSharedGeometryExclusively = true;

    // The preferred binding is the per-session factory supplied through DomBridgeSessionOptions,
    // which keeps simultaneous documents independent. This process-static factory remains only
    // as a source-compatibility fallback for composition roots that have not migrated yet. A bare
    // `new DomBridge()` with neither factory falls back to an empty view and does not pull in the
    // concrete renderer stack.
    internal static Func<ILayoutView>? LayoutViewFactory;

    private readonly Func<ILayoutView>? _layoutViewFactory;
    private ILayoutView? _layoutView;

    private ILayoutView LayoutView =>
        _layoutView ??= _layoutViewFactory?.Invoke() ?? LayoutViewFactory?.Invoke() ?? NullLayoutView.Instance;

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
    // lays out at most once per pass (one render-projection build) rather than per
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
        // The lazy build must run marked as a geometry pass, exactly as WithLayoutGeometryCache
        // marks its own. Building the snapshot creates a render projection, and that projection
        // materialises a running view transition's pseudo tree — which measures the captured
        // elements, i.e. asks for geometry again. ApplyViewTransitionRendering skips itself while a
        // pass is active, so the flag is what terminates the cycle; without it this path recursed
        // BuildSharedGeometrySnapshot → view-transition pseudo tree → getBoundingClientRect →
        // BuildSharedGeometrySnapshot until the stack overflowed.
        //
        // Latent until an await continuation could reach a geometry query mid-transition (the
        // reactions used to race away on the thread pool), and reachable from any non-pass shared
        // query, so it is guarded here rather than at the caller.
        var snapshot = _sharedGeometrySnapshot;
        if (snapshot is null)
        {
            var owner = !_layoutGeometryPassActive;
            if (owner)
                _layoutGeometryPassActive = true;
            try
            {
                snapshot = _sharedGeometrySnapshot ??= BuildSharedGeometrySnapshot();
            }
            finally
            {
                if (owner)
                    _layoutGeometryPassActive = false;
            }
        }

        return snapshot.TryGetValue(ResolveRenderSource(element), out geometry);
    }

    private static readonly IReadOnlyDictionary<DomElement, BoxGeometry> EmptySharedGeometry =
        new Dictionary<DomElement, BoxGeometry>();

    private IReadOnlyDictionary<DomElement, BoxGeometry> BuildSharedGeometrySnapshot()
    {
        // Phase-5 LayoutSnapshot endgame — the CSSOM read model uses the engine's used-value `zoom`
        // (increments 1–5), not the serialization bake. Enabling NativeZoom for the snapshot layout makes
        // GetRenderDocument skip the element-`zoom` bake (via ZoomBakeActive) and the engine scale used
        // values instead, so a zoomed element's snapshot geometry — and the unzoomed `offset*`/`client*`
        // the read path divides out of it — is correct for RELATIVE units (`%`, `em`, `rem`, `calc`) too,
        // which the bake mis-scaled (e.g. `width:50%` of a 200px CB under `zoom:2` reported `offsetWidth`
        // 50 instead of 100). Absolute lengths are unchanged (bake and engine already agreed). Thread-static
        // save/restore keeps concurrent layouts unaffected. Because the snapshot never bakes, the live
        // document stays pristine — no revert is needed (the old `_zoomSerializationRevertLog` machinery is
        // retired); the render/serialize path (called directly by capture / WPT, not via this snapshot)
        // still bakes.
        var previousNativeZoom = Broiler.Layout.Engine.NativeZoom.Enabled;
        Broiler.Layout.Engine.NativeZoom.Enabled = true;
        try
        {
            var projection = CreateRenderProjection();
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
                var projectedGeometry = LayoutView.GetGeometry(
                    projection.Document,
                    viewport,
                    _pageUrl,
                    projectedContainer =>
                    {
                        var source = projection.SourceFor(projectedContainer);
                        return source is null ? null : ResolveContentDocumentForRender(source);
                    });
                var sourceGeometry = new Dictionary<DomElement, BoxGeometry>(ReferenceEqualityComparer.Instance);
                foreach (var (projectedElement, geometry) in projectedGeometry)
                {
                    if (projection.SourceFor(projectedElement) is { } source)
                        sourceGeometry[source] = geometry;
                    else if (!ReferenceEquals(projectedElement.OwnerDocument, projection.Document))
                        // Nested-document geometry is keyed by the resolver's canonical content
                        // document, not by the outer projection. Preserve those live subdocument
                        // identities until nested documents receive their own projection mapping.
                        sourceGeometry[projectedElement] = geometry;
                }
                return sourceGeometry;
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
            Broiler.Layout.Engine.NativeZoom.Enabled = previousNativeZoom;
            // Projection preparation can populate computed-style caches for detached elements.
            // Drop them after the pass so the projection is not retained by the bridge.
            ClearComputedPropsCache();
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
