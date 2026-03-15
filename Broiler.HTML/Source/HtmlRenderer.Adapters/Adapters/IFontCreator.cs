using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Adapters;

/// <summary>
/// Interface for creating font instances without depending on the concrete
/// <see cref="RAdapter"/> type.
/// Breaks the circular dependency between <c>FontsHandler</c> and <c>RAdapter</c>.
/// </summary>
internal interface IFontCreator
{
    /// <summary>
    /// Creates a font from a family name, size, and style.
    /// </summary>
    RFont CreateFont(string family, double size, FontStyle style);

    /// <summary>
    /// Creates a font from a <see cref="RFontFamily"/>, size, and style.
    /// </summary>
    RFont CreateFont(RFontFamily family, double size, FontStyle style);
}
