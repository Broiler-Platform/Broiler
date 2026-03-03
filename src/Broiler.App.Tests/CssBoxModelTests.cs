using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
[Trait("Feature", "BoxModel")]
public class CssBoxModelTests
{
    [Fact]
    public void BuildLayoutTree_BlockChildren_StackedVertically()
    {
        var root = new DomElement("div", null, null, string.Empty);
        var child1 = new DomElement("div", null, null, string.Empty);
        var child2 = new DomElement("div", null, null, string.Empty);
        root.Children.Add(child1); child1.Parent = root;
        root.Children.Add(child2); child2.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);

        Assert.Equal(2, box.Children.Count);
        Assert.True(box.Children[1].Dimensions.Y > box.Children[0].Dimensions.Y,
            "Second block child should be below the first");
    }

    [Fact]
    public void ResolveDisplay_FromStyleDictionary()
    {
        var el = new DomElement("span", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "display", "block" }
            });

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        Assert.Equal(CssDisplay.Block, box.Display);
    }

    [Fact]
    public void ResolvePosition_FromStyleDictionary()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "position", "absolute" }
            });

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        Assert.Equal(CssPosition.Absolute, box.Position);
    }

    [Fact]
    public void ResolveFloat_FromStyleDictionary()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "float", "left" }
            });

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        Assert.Equal(CssFloat.Left, box.Float);
    }

    [Fact]
    public void ResolveClear_FromStyleDictionary()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "clear", "both" }
            });

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        Assert.Equal(CssClear.Both, box.Clear);
    }

    [Fact]
    public void ParseCssValue_PixelValue_ReturnsParsedFloat()
    {
        var result = CssBoxModel.ParseCssValue("10px", 800f, 0f);

        Assert.Equal(10f, result);
    }

    [Fact]
    public void ParseCssValue_PercentageValue_ResolvesAgainstContainer()
    {
        var result = CssBoxModel.ParseCssValue("50%", 800f, 0f);

        Assert.Equal(400f, result);
    }

    [Fact]
    public void PaddingBox_CalculatesCorrectRect()
    {
        var dims = new BoxDimensions
        {
            X = 100f, Y = 50f, Width = 200f, Height = 100f,
            Padding = new BoxEdges { Top = 10f, Right = 20f, Bottom = 10f, Left = 20f }
        };

        var rect = dims.PaddingBox();

        Assert.Equal(80f, rect.X);    // 100 - 20
        Assert.Equal(40f, rect.Y);    // 50 - 10
        Assert.Equal(240f, rect.Width);  // 200 + 20 + 20
        Assert.Equal(120f, rect.Height); // 100 + 10 + 10
    }

    [Fact]
    public void BorderBox_CalculatesCorrectRect()
    {
        var dims = new BoxDimensions
        {
            X = 100f, Y = 50f, Width = 200f, Height = 100f,
            Padding = new BoxEdges { Top = 10f, Right = 10f, Bottom = 10f, Left = 10f },
            Border = new BoxEdges { Top = 2f, Right = 2f, Bottom = 2f, Left = 2f }
        };

        var rect = dims.BorderBox();

        Assert.Equal(88f, rect.X);     // 100 - 10 - 2
        Assert.Equal(38f, rect.Y);     // 50 - 10 - 2
        Assert.Equal(224f, rect.Width);  // 200 + 10+10 + 2+2
        Assert.Equal(124f, rect.Height); // 100 + 10+10 + 2+2
    }

    [Fact]
    public void MarginBox_CalculatesCorrectRect()
    {
        var dims = new BoxDimensions
        {
            X = 100f, Y = 50f, Width = 200f, Height = 100f,
            Padding = new BoxEdges { Top = 5f, Right = 5f, Bottom = 5f, Left = 5f },
            Border = new BoxEdges { Top = 1f, Right = 1f, Bottom = 1f, Left = 1f },
            Margin = new BoxEdges { Top = 10f, Right = 10f, Bottom = 10f, Left = 10f }
        };

        var rect = dims.MarginBox();

        Assert.Equal(84f, rect.X);      // 100 - 5 - 1 - 10
        Assert.Equal(34f, rect.Y);      // 50 - 5 - 1 - 10
        Assert.Equal(232f, rect.Width);  // 200 + (5+5) + (1+1) + (10+10)
        Assert.Equal(132f, rect.Height); // 100 + (5+5) + (1+1) + (10+10)
    }

    [Fact]
    public void FloatLeft_DtAndFloatRight_Dd_PlacedSideBySide()
    {
        var dl = new DomElement("div", null, null, string.Empty);
        var dt = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "float", "left" }, { "width", "100px" }
            });
        var dd = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "float", "right" }, { "width", "200px" }
            });
        dl.Children.Add(dt); dt.Parent = dl;
        dl.Children.Add(dd); dd.Parent = dl;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(dl, 800f);

        // dt (float:left) and dd (float:right) should share the same Y position
        Assert.Equal(box.Children[0].Dimensions.Y, box.Children[1].Dimensions.Y);
        // dt should be at the left edge, dd at the right edge
        Assert.True(box.Children[1].Dimensions.X > box.Children[0].Dimensions.X,
            "Float-right child should be positioned to the right of float-left child");
    }

    [Fact]
    public void ExplicitWidth_AppliedToFloatedElement()
    {
        var parent = new DomElement("div", null, null, string.Empty);
        var child = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "float", "left" }, { "width", "100px" }
            });
        parent.Children.Add(child); child.Parent = parent;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(parent, 800f);

        Assert.Equal(100f, box.Children[0].Dimensions.Width);
    }

    [Fact]
    public void PercentageWidth_ResolvedCorrectly()
    {
        var parent = new DomElement("div", null, null, string.Empty);
        var child = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "float", "left" }, { "width", "10.638%" }
            });
        parent.Children.Add(child); child.Parent = parent;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(parent, 800f);

        // 10.638% of 800 = 85.104
        Assert.InRange(box.Children[0].Dimensions.Width, 85f, 86f);
    }

    [Fact]
    public void ExplicitHeight_AppliedToElement()
    {
        var el = new DomElement("div", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "height", "200px" }
            });

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        Assert.Equal(200f, box.Dimensions.Height);
    }

    [Fact]
    public void FloatLeft_MultipleElements_StackHorizontally()
    {
        var parent = new DomElement("div", null, null, string.Empty);
        for (int i = 0; i < 3; i++)
        {
            var li = new DomElement("div", null, null, string.Empty,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "float", "left" }, { "width", "100px" }
                });
            parent.Children.Add(li); li.Parent = parent;
        }

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(parent, 800f);

        // All three should be on the same Y, with increasing X
        Assert.Equal(box.Children[0].Dimensions.Y, box.Children[1].Dimensions.Y);
        Assert.Equal(box.Children[1].Dimensions.Y, box.Children[2].Dimensions.Y);
        Assert.True(box.Children[1].Dimensions.X > box.Children[0].Dimensions.X);
        Assert.True(box.Children[2].Dimensions.X > box.Children[1].Dimensions.X);
    }

    [Fact]
    public void FloatLeft_BlockquoteWithBorders_PositionedCorrectly()
    {
        var parent = new DomElement("div", null, null, string.Empty);
        var bq = new DomElement("blockquote", null, null, string.Empty,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "float", "left" }, { "width", "80px" },
                { "border-left-width", "5px" }, { "border-right-width", "15px" }
            });
        parent.Children.Add(bq); bq.Parent = parent;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(parent, 800f);

        // The child should have the explicit width and borders applied
        Assert.Equal(80f, box.Children[0].Dimensions.Width);
        Assert.Equal(5f, box.Children[0].Dimensions.Border.Left);
        Assert.Equal(15f, box.Children[0].Dimensions.Border.Right);
    }
}
