using System.Runtime.CompilerServices;
using Broiler.HtmlBridge;
using Broiler.Headless;

namespace Broiler.Wpt;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) composition wiring: the session options every bridge
/// the runner creates is built with, carrying the renderer-backed <see cref="HeadlessLayoutView"/>
/// as its <see cref="DomBridgeSessionOptions.LayoutViewFactory"/>, so element geometry resolves
/// through the real layout engine; and, when this host assembly loads, the run's sub-resource policy,
/// registered with the loaders that view — and the bridge itself — fetch through. Idempotent.
/// </summary>
/// <remarks>
/// <para>
/// The two belong together: the geometry view is a real layout of the real document, so it loads
/// what the document names, in a container of its own that the render's handlers never see. Until
/// the policy was registered here, that pass — and the bridge's preload scan behind it — were the
/// parts of a run that still went to the network, and a document naming an unroutable host spent
/// the per-test budget there before the guarded render began. See <see cref="WptResourceHandlers"/>.
/// </para>
/// <para>
/// The bridge used to read one process-wide layout-view factory, which this class assigned from the
/// module initializer. It now takes the factory per session, so a bridge constructed without these
/// options has no layout view and answers every geometry query with an empty map. The options hold
/// nothing else, so one instance serves every bridge, and each bridge still gets, and disposes, a
/// view of its own.
/// </para>
/// </remarks>
internal static class HeadlessLayoutViewRegistration
{
    internal static DomBridgeSessionOptions SessionOptions { get; } = new()
    {
        LayoutViewFactory = static () => new HeadlessLayoutView(),
    };

    [ModuleInitializer]
    internal static void Register() => WptResourceHandlers.Install();
}
