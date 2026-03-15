using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.Handlers;

namespace TheArtOfDev.HtmlRenderer.Adapters;

/// <summary>
/// Factory that wires concrete handler instances in the thin façade layer.
/// </summary>
/// <remarks>
/// See ADR-008, Phase 3 — "Remaining in HtmlRenderer (Thin Façade)".
/// </remarks>
internal sealed class HandlerFactory : IHandlerFactory
{
    public static readonly HandlerFactory Instance = new();

    public ISelectionHandler CreateSelectionHandler(object root) => new SelectionHandler((Core.Dom.CssBox)root);
}
