using Broiler.Headless;
using Broiler.HtmlBridge;

namespace Broiler.Cli;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) composition wiring: the session options every bridge
/// this host creates is built with, carrying the renderer-backed <see cref="HeadlessLayoutView"/> as
/// its <see cref="DomBridgeSessionOptions.LayoutViewFactory"/>, so element geometry resolves through
/// the real layout engine. The bridge itself depends only on the neutral <c>ILayoutView</c> contract
/// and never references the renderer.
/// </summary>
/// <remarks>
/// The bridge used to read one process-wide factory, which this class assigned from a module
/// initializer. It now takes the factory per session, so a bridge constructed without these options
/// has no layout view and answers every geometry query with an empty map. The options hold nothing
/// else — no network, no cookie access — so one instance serves every bridge, and each bridge still
/// gets, and disposes, a view of its own.
/// </remarks>
internal static class HeadlessLayoutViewRegistration
{
    internal static DomBridgeSessionOptions SessionOptions { get; } = new()
    {
        LayoutViewFactory = static () => new HeadlessLayoutView(),
    };
}
