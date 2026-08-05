using System;
using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.CodeEditor.Standard;

/// <summary>
/// Derives a code palette from the existing Standard theme tokens.
///
/// Deliberately a mapping rather than new tokens: <see cref="StandardThemeTokens"/>
/// makes every role <c>required</c>, so adding code-editor roles there would
/// fail to compile every theme preset in the repository. Existing theme
/// initializers keep working untouched, and a theme that wants different
/// classification colours overrides the palette on the control.
/// </summary>
public static class StandardCodeEditorPalette
{
    public static CodeEditorPalette FromTokens(StandardThemeTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        // Classification hues are derived from the theme's status and accent
        // roles so a dark theme does not get light-theme syntax colours. They
        // are lightened for dark themes, where a saturated hue on a dark
        // surface fails contrast.
        bool dark = tokens.IsDark;

        return new CodeEditorPalette
        {
            Background = tokens.Surface,
            Foreground = tokens.Text,
            GutterBackground = tokens.SurfaceAlt,
            LineNumber = tokens.TextMuted,
            CurrentLineNumber = tokens.Text,
            CurrentLineBackground = tokens.SurfaceAlt,
            Selection = tokens.AccentSoft,
            InactiveSelection = tokens.SurfaceDisabled,
            Caret = tokens.Text,
            BracketMatch = tokens.AccentSoft,
            CompositionUnderline = tokens.Text,

            Comment = Adjust(tokens.Success, dark),
            DocumentationComment = Adjust(tokens.Info, dark),
            Keyword = Adjust(tokens.Accent, dark),
            ControlKeyword = Adjust(tokens.AccentPressed, dark),
            PreprocessorKeyword = tokens.TextMuted,
            PreprocessorText = tokens.TextMuted,
            StringLiteral = Adjust(tokens.Danger, dark),
            EscapeSequence = Adjust(tokens.Warning, dark),
            CharacterLiteral = Adjust(tokens.Danger, dark),
            NumericLiteral = Adjust(tokens.Info, dark),
            Operator = tokens.Text,
            Punctuation = tokens.TextMuted,

            ErrorAdornment = tokens.Danger,
            WarningAdornment = tokens.Warning,
            InformationAdornment = tokens.Info,

            // High contrast turns off colour as the sole carrier of meaning.
            DistinguishWithoutColor = IsHighContrast(tokens),
        };
    }

    /// <summary>
    /// A theme whose surface and text sit at the extremes is a high-contrast
    /// theme, whatever it is named.
    /// </summary>
    private static bool IsHighContrast(StandardThemeTokens tokens)
    {
        double surface = Luminance(tokens.Surface);
        double text = Luminance(tokens.Text);
        return Math.Abs(surface - text) > 0.9;
    }

    private static BColor Adjust(BColor color, bool dark) => dark ? Lighten(color, 0.35) : color;

    private static BColor Lighten(BColor color, double amount) => new(
        (byte)Math.Clamp(color.R + ((255 - color.R) * amount), 0, 255),
        (byte)Math.Clamp(color.G + ((255 - color.G) * amount), 0, 255),
        (byte)Math.Clamp(color.B + ((255 - color.B) * amount), 0, 255),
        color.A);

    private static double Luminance(BColor color) =>
        ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255.0;
}
