using Broiler.Graphics;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class ComboBoxControlTests
{
    [Fact]
    public void Open_Drop_Down_Renders_After_Later_Sibling_Controls()
    {
        var host = new TestHost(new BSize(160, 120));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        BColor popupColor = BColor.FromArgb(255, 17, 29, 43);
        BColor siblingColor = BColor.FromArgb(255, 191, 67, 91);
        var comboBox = new StandardComboBox
        {
            PopupBackground = popupColor,
            PreferredSize = new BSize(80, 24),
            ItemHeight = 24,
            MaxDropDownItems = 2,
        };
        comboBox.SetItems(
        [
            new UiComboBoxItem("one", "One"),
            new UiComboBoxItem("two", "Two"),
        ]);
        Assert.True(comboBox.OpenDropDown());

        var root = new PopupOverlapRoot(comboBox, new PaintElement(siblingColor));
        session.AddRoot(root);

        BRenderList renderList = session.RenderFrame();

        int siblingIndex = FindFillRect(renderList, siblingColor);
        int popupIndex = FindRoundedFill(renderList, popupColor);
        Assert.True(siblingIndex >= 0, "Expected overlapping sibling fill command to be recorded.");
        Assert.True(popupIndex >= 0, "Expected ComboBox popup fill command to be recorded.");
        Assert.True(
            popupIndex > siblingIndex,
            $"Expected ComboBox popup command at {popupIndex} to render after overlapping sibling command at {siblingIndex}.");
    }

    private static int FindFillRect(BRenderList renderList, BColor color)
    {
        for (int index = 0; index < renderList.Commands.Count; index++)
        {
            if (renderList.Commands[index] is BRenderCommand.FillRect fill && fill.Color == color)
                return index;
        }

        return -1;
    }

    private static int FindRoundedFill(BRenderList renderList, BColor color)
    {
        for (int index = 0; index < renderList.Commands.Count; index++)
        {
            if (renderList.Commands[index] is BRenderCommand.FillRoundedRect fill && fill.Color == color)
                return index;
        }

        return -1;
    }

    private sealed class PopupOverlapRoot : UiElement
    {
        private readonly UiElement _comboBox;
        private readonly UiElement _overlappingSibling;

        public PopupOverlapRoot(UiElement comboBox, UiElement overlappingSibling)
        {
            _comboBox = comboBox;
            _overlappingSibling = overlappingSibling;
            AddChild(comboBox);
            AddChild(overlappingSibling);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _comboBox.Measure(availableSize);
            _overlappingSibling.Measure(availableSize);
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            _comboBox.Arrange(new BRect(10, 10, 80, 24));
            _overlappingSibling.Arrange(new BRect(10, 34, 80, 48));
        }
    }

    private sealed class PaintElement : UiElement
    {
        private readonly BColor _color;

        public PaintElement(BColor color)
        {
            _color = color;
        }

        protected override BSize MeasureCore(BSize availableSize) => availableSize;

        protected override void RenderCore(UiRenderContext context) =>
            context.RenderList.FillRect(Bounds, _color);
    }

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1.0;

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }
}
