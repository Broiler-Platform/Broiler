using Broiler.Graphics;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Phase3.Tests;

public sealed class Phase3FoundationTests
{
    [Fact]
    public void Standard_Window_Displays_Nested_Panels_And_Labels()
    {
        var host = new MutableHost(new BSize(320, 180), 1.25);
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(host);
        var window = new StandardWindow { Title = "Main" };
        var rootPanel = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack, Spacing = 4, Background = BColor.FromArgb(245, 248, 252) };
        var header = new StandardLabel { Text = "&File", Foreground = BColor.Blue };
        var nestedPanel = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack };
        var detail = new StandardLabel { Text = "Hello from Broiler UI", Wrapping = UiTextWrapping.Wrap };
        header.Target = nestedPanel;
        nestedPanel.AddChild(detail);
        rootPanel.AddChild(header);
        rootPanel.AddChild(nestedPanel);
        window.AddChild(rootPanel);

        session.AddRoot(window);
        BRenderList renderList = StandardRenderTraversal.Render(session);

        Assert.Equal(new BRect(0, 0, 320, 180), window.Bounds);
        Assert.Equal(new UiViewportBinding(new BSize(320, 180), 1.25), window.ViewportBinding);
        Assert.Equal('F', header.EffectiveAccessKey);
        Assert.Equal("File", header.DisplayText);
        Assert.Same(nestedPanel, header.Target);
        Assert.Contains(DrawnText(renderList), text => text == "File");
        Assert.Contains(DrawnText(renderList), text => text.Contains("Broiler", StringComparison.Ordinal));

        StandardSemanticSnapshot semantics = StandardSemanticSnapshot.Capture(session);
        UiSemanticNode semanticWindow = Assert.Single(semantics.Roots);
        Assert.Equal(UiSemanticRole.Window, semanticWindow.Role);
        Assert.Equal("Main", semanticWindow.Name);
        Assert.Equal(UiSemanticRole.Panel, Assert.Single(semanticWindow.Children).Role);
    }

    [Fact]
    public void Resize_And_Scale_Changes_Relayout_And_Redraw()
    {
        var host = new MutableHost(new BSize(240, 120), 1);
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow { Title = "Responsive" };
        var panel = new StandardPanel { LayoutMode = UiPanelLayoutMode.Overlay };
        var label = new StandardLabel { Text = "Resize proof", Direction = UiTextDirection.RightToLeft };
        panel.AddChild(label);
        window.AddChild(panel);
        session.AddRoot(window);

        StandardRenderTraversal.Render(session);
        host.Resize(new BSize(640, 360), 2);
        BRenderList resized = StandardRenderTraversal.Render(session);

        Assert.Equal(new BRect(0, 0, 640, 360), window.Bounds);
        Assert.Equal(new BRect(0, 0, 640, 360), panel.Bounds);
        Assert.Equal(new UiViewportBinding(new BSize(640, 360), 2), window.ViewportBinding);
        BRenderCommand.DrawText command = Assert.Single(resized.Commands.OfType<BRenderCommand.DrawText>());
        Assert.True(command.Origin.X > 0);
    }

    [Fact]
    public void Managed_Subwindows_Open_Stack_Activate_And_Close_Without_Native_State()
    {
        var host = new MutableHost(new BSize(300, 200), 1);
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new StandardWindow { Title = "Root" };
        var popupA = new StandardWindow { Title = "Popup A" };
        var popupB = new StandardWindow { Title = "Popup B" };
        popupA.AddChild(new StandardLabel { Text = "A" });
        popupB.AddChild(new StandardLabel { Text = "B" });
        session.AddRoot(root);

        root.OpenOwnedWindow(popupA, new BRect(10, 12, 90, 50), UiWindowKind.Popup);
        root.OpenOwnedWindow(popupB, new BRect(40, 32, 90, 50), UiWindowKind.Popup);

        Assert.Same(root, popupA.Owner);
        Assert.Same(popupB, root.Children.Last());
        Assert.True(popupB.IsActive);
        Assert.False(popupA.IsActive);

        popupA.Activate();
        Assert.Same(popupA, root.Children.Last());
        Assert.True(popupA.IsActive);
        Assert.False(popupB.IsActive);

        StandardRenderTraversal.Render(session);
        Assert.Equal(new BRect(10, 12, 90, 50), popupA.Bounds);
        Assert.True(popupA.Close(UiWindowCloseReason.User));
        Assert.True(popupA.IsClosed);
        Assert.DoesNotContain(popupA, root.OwnedWindows);
        Assert.DoesNotContain(popupA, root.Children);
    }

    [Fact]
    public void Panel_Stack_Dock_And_Overlay_Policies_Arrange_Children()
    {
        var stack = new StandardPanel { LayoutMode = UiPanelLayoutMode.Stack, Spacing = 3 };
        var first = new FixedElement(new BSize(30, 10));
        var second = new FixedElement(new BSize(20, 12));
        stack.AddChild(first);
        stack.AddChild(second);
        stack.Measure(new BSize(100, 100));
        stack.Arrange(new BRect(0, 0, 100, 100));

        Assert.Equal(new BRect(0, 0, 100, 10), first.Bounds);
        Assert.Equal(new BRect(0, 13, 100, 12), second.Bounds);

        var dock = new StandardPanel { LayoutMode = UiPanelLayoutMode.Dock };
        var left = new FixedElement(new BSize(25, 50));
        var fill = new FixedElement(new BSize(10, 10));
        dock.AddChild(left);
        dock.AddChild(fill);
        dock.SetDock(left, UiDock.Left);
        dock.SetDock(fill, UiDock.Fill);
        dock.Measure(new BSize(100, 80));
        dock.Arrange(new BRect(0, 0, 100, 80));

        Assert.Equal(new BRect(0, 0, 25, 80), left.Bounds);
        Assert.Equal(new BRect(25, 0, 75, 80), fill.Bounds);

        var overlay = new StandardPanel { LayoutMode = UiPanelLayoutMode.Overlay };
        var overlayA = new FixedElement(new BSize(4, 4));
        var overlayB = new FixedElement(new BSize(8, 8));
        overlay.AddChild(overlayA);
        overlay.AddChild(overlayB);
        overlay.Measure(new BSize(50, 40));
        overlay.Arrange(new BRect(1, 2, 50, 40));

        Assert.Equal(new BRect(1, 2, 50, 40), overlayA.Bounds);
        Assert.Equal(new BRect(1, 2, 50, 40), overlayB.Bounds);
    }

    [Fact]
    public void Label_Wraps_Trims_Aligns_Rtl_And_Exposes_Semantics()
    {
        var wrapping = new StandardLabel { Text = "alpha beta gamma delta", Wrapping = UiTextWrapping.Wrap };
        wrapping.Measure(new BSize(55, 200));
        wrapping.Arrange(new BRect(0, 0, 55, wrapping.DesiredSize.Height));
        var host = new MutableHost(new BSize(55, 200), 1);
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new StandardUiClock());
        session.AddRoot(wrapping);
        BRenderList wrapped = session.RenderFrame();

        Assert.True(wrapping.DesiredSize.Height > BTextMeasurer.GetLineHeight(wrapping.Font));
        Assert.True(wrapped.Commands.OfType<BRenderCommand.DrawText>().Count() > 1);

        var trimmed = new StandardLabel
        {
            Text = "abcdefghij",
            Trimming = UiTextTrimming.CharacterEllipsis,
            Direction = UiTextDirection.RightToLeft,
        };
        trimmed.Measure(new BSize(40, 100));
        trimmed.Arrange(new BRect(0, 0, 40, trimmed.DesiredSize.Height));
        BRenderList renderList = new();
        trimmed.Render(new UiRenderContext(renderList, session, host));

        BRenderCommand.DrawText trimmedCommand = Assert.Single(renderList.Commands.OfType<BRenderCommand.DrawText>());
        Assert.EndsWith("...", trimmedCommand.Text.Text, StringComparison.Ordinal);
        Assert.True(trimmedCommand.Origin.X >= 0);
        UiSemanticNode semantic = trimmed.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Label, semantic.Role);
        Assert.Equal("abcdefghij", semantic.Name);
        Assert.True(semantic.State.HasFlag(UiSemanticState.ReadOnly));
    }

    private static IEnumerable<string> DrawnText(BRenderList renderList) =>
        renderList.Commands.OfType<BRenderCommand.DrawText>().Select(static command => command.Text.Text);

    private sealed class MutableHost : IUiHost
    {
        public MutableHost(BSize viewportSize, double scale)
        {
            ViewportSize = viewportSize;
            Scale = scale;
        }

        public BSize ViewportSize { get; private set; }

        public double Scale { get; private set; }

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList) => Presented.Add(renderList);

        public void Resize(BSize viewportSize, double scale)
        {
            ViewportSize = viewportSize;
            Scale = scale;
        }
    }

    private sealed class FixedElement : UiElement
    {
        private readonly BSize _desiredSize;

        public FixedElement(BSize desiredSize)
        {
            _desiredSize = desiredSize;
        }

        protected override BSize MeasureCore(BSize availableSize) => _desiredSize;
    }
}
