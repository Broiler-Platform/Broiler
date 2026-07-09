using Broiler.Graphics;
using Broiler.UI.ListView.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class StandardThemeControllerTests
{
    [Fact]
    public void Apply_ReThemes_A_Real_Control_Live()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            using UiSession session = CreateSession(out ThemeHost host);
            var list = new StandardListView();
            session.AddRoot(list);

            // Baseline light.
            StandardThemeController.Apply(session, StandardThemeTokens.Light);
            Assert.Equal(StandardThemeTokens.Light.Surface, list.Background);
            Assert.Equal(StandardThemeTokens.Light.Text, list.Foreground);

            host.Invalidations.Clear();
            int themed = StandardThemeController.Apply(session, StandardThemeTokens.Dark);

            Assert.Equal(1, themed);
            Assert.Same(StandardThemeTokens.Dark, StandardControlPaint.Theme);
            Assert.Equal(StandardThemeTokens.Dark.Surface, list.Background);
            Assert.Equal(StandardThemeTokens.Dark.Text, list.Foreground);
            Assert.Equal(StandardThemeTokens.Dark.AccentSoft, list.SelectedBackground);
            Assert.NotEmpty(host.Invalidations);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    [Fact]
    public void Apply_Walks_Whole_Tree_Counting_Only_Themed_Controls()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            using UiSession session = CreateSession(out ThemeHost host);
            var root = new FakeThemed();
            var plain = new FakePlain();          // not IStandardThemedControl
            var nestedThemed = new FakeThemed();
            plain.AddChild(nestedThemed);
            root.AddChild(plain);
            session.AddRoot(root);

            host.Invalidations.Clear();
            int themed = StandardThemeController.Apply(session, StandardThemeTokens.Dark);

            Assert.Equal(2, themed);
            Assert.Same(StandardThemeTokens.Dark, root.LastTheme);
            Assert.Same(StandardThemeTokens.Dark, nestedThemed.LastTheme);
            // Plain element is still invalidated even though it is not re-themed.
            Assert.Contains(host.Invalidations, i => ReferenceEquals(i.Element, plain));
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    [Theory]
    [InlineData(UiColorScheme.Light, UiContrastPreference.NoPreference, "Light")]
    [InlineData(UiColorScheme.Dark, UiContrastPreference.NoPreference, "Dark")]
    [InlineData(UiColorScheme.Light, UiContrastPreference.More, "HighContrastLight")]
    [InlineData(UiColorScheme.Dark, UiContrastPreference.More, "HighContrastDark")]
    public void Apply_From_System_Settings_Selects_The_Matching_Preset(
        UiColorScheme scheme, UiContrastPreference contrast, string expectedName)
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            using UiSession session = CreateSession(out _);
            session.AddRoot(new FakeThemed());

            var settings = new UiSystemSettings(contrast, 1, ReducedMotion: false, UiFlowDirection.LeftToRight, scheme);
            StandardThemeController.Apply(session, settings);

            Assert.Equal(expectedName, StandardControlPaint.Theme.Name);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    private static UiSession CreateSession(out ThemeHost host)
    {
        host = new ThemeHost(new BSize(120, 80));
        return new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(new FixedClock())
            .Build(host);
    }

    private sealed class ThemeHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1.0;

        public List<UiInvalidation> Invalidations { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);

        public void Present(BRenderList renderList) { }
    }

    private sealed class FixedClock : IUiClock
    {
        public UiTimestamp Now => default;
    }

    private class FakePlain : UiElement
    {
    }

    private sealed class FakeThemed : FakePlain, IStandardThemedControl
    {
        public StandardThemeTokens? LastTheme { get; private set; }

        public void ApplyTheme(StandardThemeTokens theme) => LastTheme = theme;
    }
}
