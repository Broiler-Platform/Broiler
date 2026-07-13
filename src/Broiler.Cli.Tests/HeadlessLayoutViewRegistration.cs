using System.Runtime.CompilerServices;
using Broiler.HtmlBridge;
using Broiler.HTML.Headless;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) test-host wiring: registers the
/// renderer-backed <see cref="HeadlessLayoutView"/> as the bridge's
/// <see cref="DomBridge.LayoutViewFactory"/> when this test assembly loads, so the many
/// <c>new DomBridge()</c> geometry tests keep resolving through the real layout engine.
/// Idempotent (<c>??=</c>).
/// </summary>
internal static class HeadlessLayoutViewRegistration
{
    [ModuleInitializer]
    internal static void Register() =>
        DomBridge.LayoutViewFactory ??= static () => new HeadlessLayoutView();
}
