using Broiler.Graphics;

namespace Broiler.UI.Standard.Tests;

public sealed class StandardThemeTokensTests
{
    [Fact]
    public void Presets_Report_Their_Light_Or_Dark_Nature()
    {
        Assert.False(StandardThemeTokens.Light.IsDark);
        Assert.True(StandardThemeTokens.Dark.IsDark);
        Assert.False(StandardThemeTokens.HighContrastLight.IsDark);
        Assert.True(StandardThemeTokens.HighContrastDark.IsDark);
        Assert.Same(StandardThemeTokens.Light, StandardThemeTokens.Default);
    }

    [Fact]
    public void Light_And_Dark_Differ_On_Core_Surfaces_And_Text()
    {
        Assert.NotEqual(StandardThemeTokens.Light.Surface, StandardThemeTokens.Dark.Surface);
        Assert.NotEqual(StandardThemeTokens.Light.Text, StandardThemeTokens.Dark.Text);
        // Dark surface is darker than dark text; light surface is lighter than light text.
        Assert.True(StandardContrast.RelativeLuminance(StandardThemeTokens.Dark.Surface)
            < StandardContrast.RelativeLuminance(StandardThemeTokens.Dark.Text));
        Assert.True(StandardContrast.RelativeLuminance(StandardThemeTokens.Light.Surface)
            > StandardContrast.RelativeLuminance(StandardThemeTokens.Light.Text));
    }

    [Theory]
    [MemberData(nameof(TextPairThemes))]
    public void Text_Roles_Meet_WCAG_AA_On_Their_Surfaces(StandardThemeTokens theme)
    {
        AssertMeets(theme.Text, theme.Surface, StandardContrast.AaNormalText, theme, nameof(theme.Text));
        AssertMeets(theme.Text, theme.SurfaceAlt, StandardContrast.AaNormalText, theme, "Text/SurfaceAlt");
        AssertMeets(theme.TextMuted, theme.Surface, StandardContrast.AaNormalText, theme, nameof(theme.TextMuted));
        AssertMeets(theme.OnAccent, theme.Accent, StandardContrast.AaNormalText, theme, "OnAccent/Accent");
    }

    [Theory]
    [MemberData(nameof(TextPairThemes))]
    public void Focus_Ring_Is_Visible_Against_Its_Surface(StandardThemeTokens theme)
    {
        // Focus is a non-text UI indicator: WCAG requires 3:1 against the adjacent surface.
        AssertMeets(theme.FocusRing, theme.Surface, StandardContrast.AaLargeOrUi, theme, "FocusRing/Surface");
    }

    [Fact]
    public void Legacy_Four_Color_Constructor_Still_Works_And_Derives_Roles()
    {
        var tokens = new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red);
        Assert.Equal(BColor.Black, tokens.Surface);
        Assert.Equal(BColor.White, tokens.Text);
        Assert.Equal(BColor.Green, tokens.Accent);
        Assert.Equal(BColor.Red, tokens.FocusRing);
        // Back-compat aliases mirror the historical four-token names.
        Assert.Equal(tokens.Surface, tokens.Background);
        Assert.Equal(tokens.Text, tokens.Foreground);
        Assert.Equal(tokens.FocusRing, tokens.Focus);
    }

    [Fact]
    public void Select_Routes_System_Preferences_To_The_Right_Preset()
    {
        Assert.Same(StandardThemeTokens.Light, StandardThemeTokens.Select(UiContrastPreference.NoPreference, dark: false));
        Assert.Same(StandardThemeTokens.Dark, StandardThemeTokens.Select(UiContrastPreference.NoPreference, dark: true));
        Assert.Same(StandardThemeTokens.HighContrastLight, StandardThemeTokens.Select(UiContrastPreference.More, dark: false));
        Assert.Same(StandardThemeTokens.HighContrastDark, StandardThemeTokens.Select(UiContrastPreference.More, dark: true));
    }

    [Fact]
    public void ApplyTheme_ReColors_The_Shared_Control_Palette()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Dark);
            Assert.Same(StandardThemeTokens.Dark, StandardControlPaint.Theme);
            Assert.Equal(StandardThemeTokens.Dark.Surface, StandardControlPaint.Surface);
            Assert.Equal(StandardThemeTokens.Dark.Text, StandardControlPaint.Text);
            Assert.Equal(StandardThemeTokens.Dark.Accent, StandardControlPaint.Accent);
            Assert.Equal(StandardThemeTokens.Dark.FocusRing, StandardControlPaint.Focus);

            StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
            Assert.Equal(BColor.White, StandardControlPaint.Surface);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    public static TheoryData<StandardThemeTokens> TextPairThemes() => new()
    {
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    };

    private static void AssertMeets(BColor foreground, BColor background, double target, StandardThemeTokens theme, string pair)
    {
        double ratio = StandardContrast.Ratio(foreground, background);
        Assert.True(
            ratio >= target,
            $"{theme.Name}: {pair} contrast {ratio:0.00}:1 is below the required {target:0.0}:1.");
    }
}
