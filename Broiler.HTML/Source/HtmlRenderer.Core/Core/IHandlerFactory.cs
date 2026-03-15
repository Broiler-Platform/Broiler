namespace TheArtOfDev.HtmlRenderer.Core;

/// <summary>
/// Factory interface for creating handler instances in the orchestration layer.
/// Breaks the dependency between <c>HtmlContainerInt</c> (in Orchestration)
/// and concrete handler constructors (in the façade).
/// </summary>
/// <remarks>
/// See ADR-008, Phase 3 prerequisites, item 3.
/// The <paramref name="root"/> parameter in <see cref="CreateSelectionHandler"/>
/// is typed as <c>object</c> because the concrete type (<c>CssBox</c>) resides
/// in HtmlRenderer.Dom, which HtmlRenderer.Core cannot reference.
/// </remarks>
internal interface IHandlerFactory
{
    /// <summary>
    /// Creates a new selection handler for the given root box.
    /// </summary>
    /// <param name="root">The root <c>CssBox</c> (passed as object to avoid L4a dependency).</param>
    ISelectionHandler CreateSelectionHandler(object root);
}
