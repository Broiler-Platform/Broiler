using System.Runtime.CompilerServices;
using Broiler.HtmlBridge;
using Broiler.HTML.Headless;

namespace Broiler.Wpt;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) composition wiring: registers the
/// renderer-backed <see cref="HeadlessLayoutView"/> as the bridge's
/// <see cref="DomBridge.LayoutViewFactory"/> when this host assembly loads, so element
/// geometry resolves through the real layout engine. Idempotent (<c>??=</c>).
/// </summary>
internal static class HeadlessLayoutViewRegistration
{
    [ModuleInitializer]
    internal static void Register() =>
        DomBridge.LayoutViewFactory ??= static () => new HeadlessLayoutView();
}
