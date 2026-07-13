using System.Runtime.CompilerServices;
using Broiler.HtmlBridge;
using Broiler.HTML.Headless;

namespace Broiler.Cli;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) composition wiring: registers the
/// renderer-backed <see cref="HeadlessLayoutView"/> as the bridge's
/// <see cref="DomBridge.LayoutViewFactory"/> when this host assembly loads, so element
/// geometry resolves through the real layout engine. The bridge itself depends only on the
/// neutral <c>ILayoutView</c> contract and never references the renderer. Idempotent
/// (<c>??=</c>) so overlapping host assemblies in one process register the same view once.
/// </summary>
internal static class HeadlessLayoutViewRegistration
{
    [ModuleInitializer]
    internal static void Register() =>
        DomBridge.LayoutViewFactory ??= static () => new HeadlessLayoutView();
}
