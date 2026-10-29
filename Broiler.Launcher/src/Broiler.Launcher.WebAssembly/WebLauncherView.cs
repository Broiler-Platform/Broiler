using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Launcher.Browser;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Label.Standard;

namespace Broiler.Launcher.WebAssembly;

internal sealed class WebLauncherView : UiElement
{
    private readonly BrowserLauncher _launcher;
    private readonly StandardLabel _title = StandardLabel.Title("Choose your app");
    private readonly StandardLabel _subtitle = StandardLabel.Muted("Documents and spreadsheets in your browser.");
    private readonly StandardButton _theme, _tabs;
    private readonly List<(StandardLabel Name, StandardLabel Detail, StandardLabel Version, StandardButton Open)> _cards = [];
    private readonly List<BRect> _bounds = [];
    private bool _dark;

    internal WebLauncherView(BrowserLauncher launcher, BrowserDeploymentInfo deployment, Action toggleTheme)
    {
        _launcher = launcher;
        AddChild(_title); AddChild(_subtitle);
        _theme = Button("", toggleTheme);
        _tabs = Button("", () => { launcher.SetOpenInNewTab(!launcher.OpenInNewTab); UpdatePreferences(); });
        AddChild(_theme); AddChild(_tabs);
        foreach (var product in launcher.Products) {
            string detail = product.Id switch {
                "writer" => "Create and edit documents.", "cell" => "Work with CSV spreadsheets.",
                _ => "Available in the desktop edition.",
            };
            string version = product.Id switch { "writer" => deployment.WriterVersion, "cell" => deployment.CellVersion, _ => "Desktop app" };
            var name = StandardLabel.Subtitle(product.Name);
            var description = new StandardLabel(detail) { Font = new BFontStyle("Segoe UI", 13) };
            var release = StandardLabel.Caption(version);
            var open = Button(product.CanLaunch ? "Open " + product.Name.Replace("Broiler ", "") : "Desktop edition", () => launcher.Launch(product.Id));
            open.IsEnabled = product.CanLaunch;
            _cards.Add((name, description, release, open)); _bounds.Add(default);
            AddChild(name); AddChild(description); AddChild(release); AddChild(open);
        }
        UpdatePreferences();
    }
    private static StandardButton Button(string text, Action action)
    {
        var button = new StandardButton { Text = text, AccessibleName = text, CornerRadius = 6 };
        button.Clicked += (_, _) => action(); return button;
    }
    private void UpdatePreferences()
    {
        _theme.Text = _launcher.Theme == "dark" ? "Light theme" : "Dark theme";
        _theme.AccessibleName = _theme.Text;
        _tabs.Text = _launcher.OpenInNewTab ? "Apps: new tab" : "Apps: same tab";
        _tabs.AccessibleName = _tabs.Text;
        Invalidate(UiInvalidationKind.Render);
    }
    internal void SetTheme(bool dark) { _dark = dark; UpdatePreferences(); }
    protected override BSize MeasureCore(BSize availableSize)
    {
        foreach (var child in Children) child.Measure(availableSize);
        return availableSize;
    }
    protected override void ArrangeCore(BRect rectangle)
    {
        double width = rectangle.Width, x = rectangle.X + 24, y = rectangle.Y;
        _title.Arrange(new(x, y + 22, width - 48, 32));
        _subtitle.Arrange(new(x, y + 62, width - 48, 26));
        _theme.Arrange(new(x, y + 104, 130, 34)); _tabs.Arrange(new(x + 144, y + 104, Math.Min(180, width - 192), 34));
        int columns = width >= 700 ? 2 : 1;
        double cardWidth = (width - 48 - (columns - 1) * 16) / columns;
        for (int index = 0; index < _cards.Count; index++) {
            var card = new BRect(x + index % columns * (cardWidth + 16), y + 162 + index / columns * 182, cardWidth, 166);
            _bounds[index] = card;
            var controls = _cards[index];
            controls.Name.Arrange(new(card.X + 20, card.Y + 14, cardWidth - 40, 28));
            controls.Detail.Arrange(new(card.X + 20, card.Y + 48, cardWidth - 40, 24));
            controls.Version.Arrange(new(card.X + 20, card.Y + 78, cardWidth - 40, 20));
            controls.Open.Arrange(new(card.X + 20, card.Y + 114, Math.Min(170, cardWidth - 40), 34));
        }
    }
    protected override void RenderCore(UiRenderContext context)
    {
        for (int index = 0; index < _bounds.Count; index++) {
            bool enabled = _launcher.Products[index].CanLaunch;
            BColor surface = _dark ? BColor.FromArgb(255, enabled ? (byte)29 : (byte)21, enabled ? (byte)41 : (byte)29, enabled ? (byte)57 : (byte)40)
                : enabled ? BColor.White : BColor.FromArgb(255, 235, 239, 244);
            context.RenderList.FillRoundedRect(_bounds[index], surface, 10, 10);
            context.RenderList.StrokeRoundedRect(_bounds[index], _dark ? BColor.FromArgb(255, 61, 77, 98) : BColor.FromArgb(255, 210, 220, 232), 10, 10, 1);
        }
        base.RenderCore(context);
    }
}
