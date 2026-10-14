using Broiler.HtmlBridge;
using Broiler.Headless;

namespace Broiler.Engines.Baseline;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) composition wiring: the session options every bridge
/// this host creates is built with, carrying the renderer-backed <see cref="HeadlessLayoutView"/> as
/// its <see cref="DomBridgeSessionOptions.LayoutViewFactory"/>, so element geometry resolves through
/// the real layout engine.
/// </summary>
/// <remarks>
/// The bridge used to read one process-wide factory, which this class assigned from a module
/// initializer; it now takes the factory per session. The options hold nothing else, so one instance
/// serves every bridge, and each bridge still gets, and disposes, a view of its own.
/// </remarks>
internal static class HeadlessLayoutViewRegistration
{
    internal static DomBridgeSessionOptions SessionOptions { get; } = new()
    {
        LayoutViewFactory = static () => new HeadlessLayoutView(),
    };
}
