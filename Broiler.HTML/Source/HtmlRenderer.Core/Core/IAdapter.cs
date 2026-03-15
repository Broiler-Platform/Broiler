using System.Drawing;
using System.IO;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Core.Entities;

namespace TheArtOfDev.HtmlRenderer.Core;

/// <summary>
/// Interface abstracting the platform adapter for use by the orchestration layer.
/// Breaks the dependency between <c>HtmlContainerInt</c> (in Orchestration)
/// and the concrete <c>RAdapter</c> class (in the façade).
/// </summary>
/// <remarks>
/// See ADR-008, Phase 3. Extends <see cref="IColorResolver"/> so the
/// orchestrator can pass it directly to <c>CssParser</c>.
/// </remarks>
internal interface IAdapter : IColorResolver
{
    /// <summary>
    /// The default CSS data for the platform.
    /// </summary>
    CssData DefaultCssData { get; }

    /// <summary>
    /// Gets a cached font for the specified family, size, and style.
    /// </summary>
    RFont GetFont(string family, double size, FontStyle style);

    /// <summary>
    /// Converts a platform-specific image object to an <see cref="RImage"/>.
    /// </summary>
    RImage ConvertImage(object image);

    /// <summary>
    /// Creates an <see cref="RImage"/> from a stream.
    /// </summary>
    RImage ImageFromStream(Stream stream);

    /// <summary>
    /// Gets the loading placeholder image.
    /// </summary>
    RImage GetLoadingImage();

    /// <summary>
    /// Gets the error placeholder image.
    /// </summary>
    RImage GetLoadingFailedImage();
}
