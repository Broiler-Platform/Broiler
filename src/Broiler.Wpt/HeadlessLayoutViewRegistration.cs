using System.Runtime.CompilerServices;
using Broiler.HtmlBridge;
using Broiler.HTML.Headless;

namespace Broiler.Wpt;

/// <summary>
/// Phase 1 (HtmlBridge project-graph repair) composition wiring: registers the
/// renderer-backed <see cref="HeadlessLayoutView"/> as the bridge's
/// <see cref="DomBridge.LayoutViewFactory"/> when this host assembly loads, so element
/// geometry resolves through the real layout engine, and registers the run's sub-resource
/// policy with the loaders that view — and the bridge itself — fetch through. Idempotent.
/// </summary>
/// <remarks>
/// The two belong together: the geometry view is a real layout of the real document, so it loads
/// what the document names, in a container of its own that the render's handlers never see. Until
/// the policy was registered here, that pass — and the bridge's preload scan behind it — were the
/// parts of a run that still went to the network, and a document naming an unroutable host spent
/// the per-test budget there before the guarded render began. See <see cref="WptResourceHandlers"/>.
/// </remarks>
internal static class HeadlessLayoutViewRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        DomBridge.LayoutViewFactory ??= static () => new HeadlessLayoutView();
        WptResourceHandlers.Install();
    }
}
