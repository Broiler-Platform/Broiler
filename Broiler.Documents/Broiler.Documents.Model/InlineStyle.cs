using Broiler.Graphics;

namespace Broiler.Documents.Model;

/// <summary>
/// A fully resolved inline character style. Every attribute has a concrete value;
/// <see cref="Default"/> means "no explicit formatting" (inherit the control's
/// default appearance). The first-release attribute set is fixed by ADR 0014.
/// </summary>
public readonly record struct InlineStyle
{
    /// <summary>Font family name, or <see langword="null"/> for the default family.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Font size in logical units, or <see langword="null"/> for the default size.</summary>
    public float? FontSize { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    /// <summary>Foreground color, or <see cref="BColor.Empty"/> for the default color.</summary>
    public BColor Foreground { get; init; }

    /// <summary>Background/highlight color, or <see cref="BColor.Empty"/> for none.</summary>
    public BColor Background { get; init; }

    /// <summary>Link target, or <see langword="null"/> when the run is not a link.</summary>
    public string? LinkHref { get; init; }

    /// <summary>The unformatted default style (<c>default(InlineStyle)</c>).</summary>
    public static InlineStyle Default => default;

    /// <summary>True when the run carries link metadata.</summary>
    public bool IsLink => !string.IsNullOrEmpty(LinkHref);
}
