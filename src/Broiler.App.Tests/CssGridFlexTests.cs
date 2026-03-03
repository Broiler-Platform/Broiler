using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class CssGridFlexTests
{
    // --- Flexbox tests ---

    [Fact]
    public void ResolveDisplay_Flex_FromStyleDictionary()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "display", "flex" } });
        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);
        Assert.Equal(CssDisplay.Flex, box.Display);
    }

    [Fact]
    public void ResolveDisplay_Grid_FromStyleDictionary()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "display", "grid" } });
        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);
        Assert.Equal(CssDisplay.Grid, box.Display);
    }

    [Fact]
    public void FlexLayout_Row_ChildrenArrangedHorizontally()
    {
        var root = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "display", "flex" } });
        var child1 = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "width", "100px" } });
        var child2 = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "width", "100px" } });
        root.Children.Add(child1); child1.Parent = root;
        root.Children.Add(child2); child2.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);
        Assert.Equal(2, box.Children.Count);
        Assert.True(box.Children[1].Dimensions.X > box.Children[0].Dimensions.X,
            "Second flex child should be to the right of the first in row direction");
    }

    [Fact]
    public void FlexLayout_Column_ChildrenArrangedVertically()
    {
        var root = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "display", "flex" }, { "flex-direction", "column" } });
        var child1 = new DomElement("div", null, null, string.Empty);
        var child2 = new DomElement("div", null, null, string.Empty);
        root.Children.Add(child1); child1.Parent = root;
        root.Children.Add(child2); child2.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);
        Assert.True(box.Children[1].Dimensions.Y > box.Children[0].Dimensions.Y,
            "Second flex child should be below the first in column direction");
    }

    [Fact]
    public void FlexLayout_Gap_AddsSpaceBetweenChildren()
    {
        var root = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "display", "flex" }, { "gap", "20px" } });
        var child1 = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "width", "100px" } });
        var child2 = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "width", "100px" } });
        root.Children.Add(child1); child1.Parent = root;
        root.Children.Add(child2); child2.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);
        Assert.Equal(20f, box.Gap);
    }

    [Fact]
    public void FlexLayout_JustifyContent_ResolvedFromStyle()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "display", "flex" }, { "justify-content", "center" } });
        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);
        Assert.Equal(JustifyContent.Center, box.JustifyContent);
    }

    [Fact]
    public void FlexLayout_AlignItems_ResolvedFromStyle()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "display", "flex" }, { "align-items", "center" } });
        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);
        Assert.Equal(AlignItems.Center, box.AlignItems);
    }

    // --- Grid tests ---

    [Fact]
    public void GridLayout_ChildrenPlacedInCells()
    {
        var root = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { { "display", "grid" }, { "grid-template-columns", "1fr 1fr" } });
        var child1 = new DomElement("div", null, null, string.Empty);
        var child2 = new DomElement("div", null, null, string.Empty);
        root.Children.Add(child1); child1.Parent = root;
        root.Children.Add(child2); child2.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);
        Assert.Equal(2, box.Children.Count);
        Assert.True(box.Children[1].Dimensions.X > box.Children[0].Dimensions.X,
            "Second grid child should be in the second column");
    }

    [Fact]
    public void GridTrackSize_FractionUnit_Created()
    {
        var track = new GridTrackSize(1f, GridTrackUnit.Fraction);
        Assert.Equal(1f, track.Value);
        Assert.Equal(GridTrackUnit.Fraction, track.Unit);
    }

    [Fact]
    public void GridTrackSize_PixelUnit_Created()
    {
        var track = new GridTrackSize(200f, GridTrackUnit.Pixel);
        Assert.Equal(200f, track.Value);
        Assert.Equal(GridTrackUnit.Pixel, track.Unit);
    }
}
