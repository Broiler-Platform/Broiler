using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.HtmlBridge;

/// <summary>
/// <see cref="DomBridge"/>'s implementation of <see cref="IFetchHost"/>, the narrow contract the
/// extracted <see cref="Broiler.HtmlBridge.Dom.Features.FetchBinding"/> feature module consumes
/// (HtmlBridge complexity-reduction roadmap Phase 3, P3.11). The single member is an explicit
/// interface implementation, so it does not widen the public <c>DomBridge</c> surface.
/// </summary>
public sealed partial class DomBridge : IFetchHost
{
    string IFetchHost.PageUrl => _pageUrl;
}
