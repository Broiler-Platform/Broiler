using Broiler.HtmlBridge;
using Broiler.Headless;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) test-host wiring: the session options the tests'
/// bridges are built with, carrying the renderer-backed <see cref="HeadlessLayoutView"/> as their
/// <see cref="DomBridgeSessionOptions.LayoutViewFactory"/>, so geometry tests keep resolving through
/// the real layout engine.
/// </summary>
/// <remarks>
/// The bridge used to read one process-wide factory, which this class assigned from a module
/// initializer so that every bare <c>new DomBridge()</c> here got the view. It now takes the factory
/// per session, so each test bridge passes these options instead; a bridge built without them has
/// no layout view and answers every geometry query with an empty map.
/// </remarks>
internal static class HeadlessLayoutViewRegistration
{
    internal static DomBridgeSessionOptions SessionOptions { get; } = new()
    {
        LayoutViewFactory = static () => new HeadlessLayoutView(),
    };
}
