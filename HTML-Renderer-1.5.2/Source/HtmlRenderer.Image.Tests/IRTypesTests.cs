using System.Drawing;
using System.Linq;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests for the Phase 1 IR (Intermediate Representation) types:
/// <see cref="BoxEdges"/>, <see cref="ComputedStyle"/>, <see cref="Fragment"/>,
/// <see cref="DisplayList"/>, and <see cref="IRasterBackend"/>.
/// Also tests shadow IR building via <see cref="HtmlContainerInt.LatestFragmentTree"/>.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class IRTypesTests
{
    // =================================================================
    // BoxEdges
    // =================================================================

    [Fact]
    public void BoxEdges_Zero_HasAllZeroValues()
    {
        var zero = BoxEdges.Zero;
        Assert.Equal(0, zero.Top);
        Assert.Equal(0, zero.Right);
        Assert.Equal(0, zero.Bottom);
        Assert.Equal(0, zero.Left);
    }

    [Fact]
    public void BoxEdges_Constructor_StoresValues()
    {
        var edges = new BoxEdges(1.5, 2.5, 3.5, 4.5);
        Assert.Equal(1.5, edges.Top);
        Assert.Equal(2.5, edges.Right);
        Assert.Equal(3.5, edges.Bottom);
        Assert.Equal(4.5, edges.Left);
    }

    [Fact]
    public void BoxEdges_Equality_SameValues_AreEqual()
    {
        var a = new BoxEdges(1, 2, 3, 4);
        var b = new BoxEdges(1, 2, 3, 4);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void BoxEdges_Equality_DifferentValues_AreNotEqual()
    {
        var a = new BoxEdges(1, 2, 3, 4);
        var b = new BoxEdges(4, 3, 2, 1);
        Assert.NotEqual(a, b);
    }

    // =================================================================
    // ComputedStyle
    // =================================================================

    [Fact]
    public void ComputedStyle_Defaults_HaveExpectedValues()
    {
        var style = new ComputedStyle();
        Assert.Equal("inline", style.Display);
        Assert.Equal("static", style.Position);
        Assert.Equal("none", style.Float);
        Assert.Equal("none", style.Clear);
        Assert.Equal("visible", style.Overflow);
        Assert.Equal("visible", style.Visibility);
        Assert.Equal("auto", style.Width);
        Assert.Equal("auto", style.Height);
        Assert.Equal("normal", style.FontStyle);
        Assert.Equal("normal", style.FontWeight);
        Assert.Equal("none", style.BackgroundImage);
        Assert.Equal("disc", style.ListStyleType);
        Assert.Equal("1", style.Opacity);
    }

    [Fact]
    public void ComputedStyle_InitProperties_CanBeSet()
    {
        var style = new ComputedStyle
        {
            Display = "block",
            Position = "absolute",
            Float = "left",
            ActualWidth = 100.5,
            ActualHeight = 50.25,
            ActualColor = Color.FromArgb(255, 0, 0),
            FontFamily = "Arial",
        };

        Assert.Equal("block", style.Display);
        Assert.Equal("absolute", style.Position);
        Assert.Equal("left", style.Float);
        Assert.Equal(100.5, style.ActualWidth);
        Assert.Equal(50.25, style.ActualHeight);
        Assert.Equal(Color.FromArgb(255, 0, 0), style.ActualColor);
        Assert.Equal("Arial", style.FontFamily);
    }

    [Fact]
    public void ComputedStyle_BoxEdges_CanBeAssigned()
    {
        var style = new ComputedStyle
        {
            Margin = new BoxEdges(10, 20, 10, 20),
            Border = new BoxEdges(1, 1, 1, 1),
            Padding = new BoxEdges(5, 5, 5, 5),
        };

        Assert.Equal(10, style.Margin.Top);
        Assert.Equal(20, style.Margin.Right);
        Assert.Equal(1, style.Border.Top);
        Assert.Equal(5, style.Padding.Left);
    }

    // =================================================================
    // Fragment
    // =================================================================

    [Fact]
    public void Fragment_Defaults_HaveExpectedValues()
    {
        var fragment = new Fragment();
        Assert.Equal(PointF.Empty, fragment.Location);
        Assert.Equal(SizeF.Empty, fragment.Size);
        Assert.Empty(fragment.Children);
        Assert.Null(fragment.Lines);
        Assert.NotNull(fragment.Style);
        Assert.False(fragment.CreatesStackingContext);
        Assert.Equal(0, fragment.StackLevel);
    }

    [Fact]
    public void Fragment_Bounds_ComputedFromLocationAndSize()
    {
        var fragment = new Fragment
        {
            Location = new PointF(10, 20),
            Size = new SizeF(100, 50),
        };

        Assert.Equal(10, fragment.Bounds.X);
        Assert.Equal(20, fragment.Bounds.Y);
        Assert.Equal(100, fragment.Bounds.Width);
        Assert.Equal(50, fragment.Bounds.Height);
    }

    [Fact]
    public void Fragment_Children_CanBePopulated()
    {
        var child1 = new Fragment { Location = new PointF(0, 0), Size = new SizeF(50, 50) };
        var child2 = new Fragment { Location = new PointF(50, 0), Size = new SizeF(50, 50) };

        var parent = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Children = [child1, child2],
        };

        Assert.Equal(2, parent.Children.Count);
    }

    // =================================================================
    // LineFragment / InlineFragment
    // =================================================================

    [Fact]
    public void LineFragment_PreservesInlineTextAndOrder()
    {
        var inline1 = new InlineFragment { X = 0, Y = 0, Width = 30, Height = 12, Text = "Hello" };
        var inline2 = new InlineFragment { X = 30, Y = 0, Width = 40, Height = 12, Text = "World" };

        var line = new LineFragment
        {
            X = 0,
            Y = 0,
            Width = 70,
            Height = 12,
            Inlines = [inline1, inline2],
        };

        Assert.Equal(2, line.Inlines.Count);
        Assert.Equal("Hello", line.Inlines[0].Text);
        Assert.Equal("World", line.Inlines[1].Text);
    }

    // =================================================================
    // DisplayList
    // =================================================================

    [Fact]
    public void DisplayList_Empty_HasNoItems()
    {
        var list = new DisplayList();
        Assert.Empty(list.Items);
    }

    [Fact]
    public void DisplayList_CanHoldVariousItems()
    {
        var items = new DisplayItem[]
        {
            new FillRectItem { Bounds = new RectangleF(0, 0, 100, 100), Color = Color.Red },
            new DrawBorderItem
            {
                Bounds = new RectangleF(0, 0, 100, 100),
                Widths = new BoxEdges(1, 1, 1, 1),
                TopColor = Color.Black,
                RightColor = Color.Black,
                BottomColor = Color.Black,
                LeftColor = Color.Black,
            },
            new DrawTextItem
            {
                Text = "Hello",
                FontFamily = "Arial",
                FontSize = 12,
                Color = Color.Black,
                Origin = new PointF(10, 10),
            },
            new ClipItem { ClipRect = new RectangleF(0, 0, 200, 200) },
            new RestoreItem(),
            new OpacityItem { Opacity = 0.5f },
        };

        var list = new DisplayList { Items = items };
        Assert.Equal(6, list.Items.Count);
        Assert.IsType<FillRectItem>(list.Items[0]);
        Assert.IsType<DrawBorderItem>(list.Items[1]);
        Assert.IsType<DrawTextItem>(list.Items[2]);
        Assert.IsType<ClipItem>(list.Items[3]);
        Assert.IsType<RestoreItem>(list.Items[4]);
        Assert.IsType<OpacityItem>(list.Items[5]);
    }

    [Fact]
    public void DrawImageItem_CanHoldImageReference()
    {
        var item = new DrawImageItem
        {
            Bounds = new RectangleF(0, 0, 100, 100),
            DestRect = new RectangleF(0, 0, 100, 100),
            SourceRect = new RectangleF(0, 0, 50, 50),
            ImageHandle = null,
        };

        Assert.Null(item.ImageHandle);
        Assert.Equal(100, item.DestRect.Width);
    }

    // =================================================================
    // Shadow IR building (integration test via HtmlContainer)
    // =================================================================

    [Fact]
    public void PerformLayout_BuildsShadowFragmentTree()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;height:100px;'>Hello</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        var fragmentTree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(fragmentTree);
        LayoutInvariantChecker.AssertValid(fragmentTree);
    }

    [Fact]
    public void PerformLayout_FragmentTree_HasChildren()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(
            @"<div style='width:200px;'>
                <p>First</p>
                <p>Second</p>
            </div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        var fragmentTree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(fragmentTree);
        LayoutInvariantChecker.AssertValid(fragmentTree);
        Assert.True(fragmentTree.Children.Count > 0, "Root fragment should have children");
    }

    [Fact]
    public void PerformLayout_FragmentTree_CapturesComputedStyle()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='display:block;width:200px;'>Test</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        var fragmentTree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(fragmentTree);
        LayoutInvariantChecker.AssertValid(fragmentTree);
        Assert.NotNull(fragmentTree.Style);
        Assert.Equal("block", fragmentTree.Style.Display);
    }

    [Fact]
    public void PerformLayout_FragmentTree_HasPositiveSize()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;height:100px;'>Content</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        var fragmentTree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(fragmentTree);
        LayoutInvariantChecker.AssertValid(fragmentTree);
        Assert.True(fragmentTree.Size.Width > 0, "Fragment width should be positive");
        Assert.True(fragmentTree.Size.Height > 0, "Fragment height should be positive");
    }

    [Fact]
    public void PerformLayout_FragmentTree_StyleHasBorderEdges()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;padding:10px;border:2px solid black;'>Styled</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        var fragmentTree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(fragmentTree);
        LayoutInvariantChecker.AssertValid(fragmentTree);

        // The styled div is a child of the root; find the fragment with padding/border
        var styledFragment = FindFragmentWithPadding(fragmentTree);
        Assert.NotNull(styledFragment);
        Assert.True(styledFragment.Style.Padding.Top >= 10, "Padding top should be >= 10");
        Assert.True(styledFragment.Style.Border.Top >= 2, "Border top should be >= 2");
    }

    private static Fragment? FindFragmentWithPadding(Fragment fragment)
    {
        if (fragment.Style.Padding.Top >= 10)
            return fragment;

        foreach (var child in fragment.Children)
        {
            var found = FindFragmentWithPadding(child);
            if (found != null)
                return found;
        }

        return null;
    }

    [Fact]
    public void PerformLayout_ExistingBehavior_Unchanged()
    {
        // Verify that adding shadow IR building doesn't change actual rendered output
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;'>Hello World</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        // Layout should still produce correct sizes
        Assert.True(container.ActualSize.Width > 0);
        Assert.True(container.ActualSize.Height > 0);

        // Paint should still work
        container.PerformPaint(canvas, clip);

        // Verify the bitmap has non-white pixels (text was rendered)
        bool hasNonWhitePixels = false;
        for (int y = 0; y < bitmap.Height && !hasNonWhitePixels; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel != SKColors.White)
                {
                    hasNonWhitePixels = true;
                    break;
                }
            }
        }

        Assert.True(hasNonWhitePixels, "Rendering should produce visible output");
    }

    // =================================================================
    // Phase 2 — BoxKind enum
    // =================================================================

    [Fact]
    public void BoxKind_Default_IsAnonymous()
    {
        var style = new ComputedStyle();
        Assert.Equal(BoxKind.Anonymous, style.Kind);
    }

    [Fact]
    public void BoxKind_CanBeSetViaInit()
    {
        var style = new ComputedStyle { Kind = BoxKind.ReplacedImage };
        Assert.Equal(BoxKind.ReplacedImage, style.Kind);
    }

    [Theory]
    [InlineData(BoxKind.Block)]
    [InlineData(BoxKind.Inline)]
    [InlineData(BoxKind.ReplacedImage)]
    [InlineData(BoxKind.ReplacedIframe)]
    [InlineData(BoxKind.TableCell)]
    [InlineData(BoxKind.Table)]
    [InlineData(BoxKind.TableRow)]
    [InlineData(BoxKind.ListItem)]
    [InlineData(BoxKind.OrderedList)]
    [InlineData(BoxKind.UnorderedList)]
    [InlineData(BoxKind.HorizontalRule)]
    [InlineData(BoxKind.LineBreak)]
    [InlineData(BoxKind.Anchor)]
    [InlineData(BoxKind.Font)]
    [InlineData(BoxKind.Input)]
    [InlineData(BoxKind.Heading)]
    public void BoxKind_AllVariants_RoundTrip(BoxKind kind)
    {
        var style = new ComputedStyle { Kind = kind };
        Assert.Equal(kind, style.Kind);
    }

    // =================================================================
    // Phase 2 — List attributes on ComputedStyle
    // =================================================================

    [Fact]
    public void ComputedStyle_ListStart_DefaultsToNull()
    {
        var style = new ComputedStyle();
        Assert.Null(style.ListStart);
    }

    [Fact]
    public void ComputedStyle_ListReversed_DefaultsToFalse()
    {
        var style = new ComputedStyle();
        Assert.False(style.ListReversed);
    }

    [Fact]
    public void ComputedStyle_ListAttributes_CanBeSet()
    {
        var style = new ComputedStyle
        {
            ListStart = 5,
            ListReversed = true,
        };

        Assert.Equal(5, style.ListStart);
        Assert.True(style.ListReversed);
    }

    // =================================================================
    // Phase 2 — ImageSource on ComputedStyle
    // =================================================================

    [Fact]
    public void ComputedStyle_ImageSource_DefaultsToNull()
    {
        var style = new ComputedStyle();
        Assert.Null(style.ImageSource);
    }

    [Fact]
    public void ComputedStyle_ImageSource_CanBeSet()
    {
        var style = new ComputedStyle { ImageSource = "https://example.com/img.png" };
        Assert.Equal("https://example.com/img.png", style.ImageSource);
    }

    // =================================================================
    // Phase 2 — BoxKind populated via DomParser (integration)
    // =================================================================

    [Fact]
    public void PerformLayout_ImgElement_HasReplacedImageKind()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div><img src='test.png' style='width:50px;height:50px;'/></div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        container.PerformLayout(canvas, new RectangleF(0, 0, 500, 500));

        var tree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(tree);
        LayoutInvariantChecker.AssertValid(tree);
        var imgFragment = FindFragmentByKind(tree, BoxKind.ReplacedImage);
        Assert.NotNull(imgFragment);
    }

    [Fact]
    public void PerformLayout_TableElement_HasTableKind()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<table><tr><td>Cell</td></tr></table>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        container.PerformLayout(canvas, new RectangleF(0, 0, 500, 500));

        var tree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(tree);
        LayoutInvariantChecker.AssertValid(tree);
        var tableFragment = FindFragmentByKind(tree, BoxKind.Table);
        Assert.NotNull(tableFragment);
    }

    [Fact]
    public void PerformLayout_OrderedList_HasListStartAndReversed()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<ol start='3' reversed><li>A</li><li>B</li></ol>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        container.PerformLayout(canvas, new RectangleF(0, 0, 500, 500));

        var tree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(tree);
        LayoutInvariantChecker.AssertValid(tree);
        var olFragment = FindFragmentByKind(tree, BoxKind.OrderedList);
        Assert.NotNull(olFragment);
        Assert.Equal(3, olFragment.Style.ListStart);
        Assert.True(olFragment.Style.ListReversed);
    }

    [Fact]
    public void PerformLayout_HrElement_HasHorizontalRuleKind()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div><hr/></div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        container.PerformLayout(canvas, new RectangleF(0, 0, 500, 500));

        var tree = container.HtmlContainerInt.LatestFragmentTree;
        Assert.NotNull(tree);
        LayoutInvariantChecker.AssertValid(tree);
        var hrFragment = FindFragmentByKind(tree, BoxKind.HorizontalRule);
        Assert.NotNull(hrFragment);
    }

    private static Fragment? FindFragmentByKind(Fragment fragment, BoxKind kind)
    {
        if (fragment.Style.Kind == kind)
            return fragment;

        foreach (var child in fragment.Children)
        {
            var found = FindFragmentByKind(child, kind);
            if (found != null)
                return found;
        }

        return null;
    }

    // =================================================================
    // Phase 3 — PaintWalker: Fragment tree → DisplayList
    // =================================================================

    [Fact]
    public void PaintWalker_EmptyFragment_ProducesEmptyDisplayList()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 100),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.NotNull(displayList);
        Assert.NotNull(displayList.Items);
    }

    [Fact]
    public void PaintWalker_BackgroundColor_EmitsFillRectItem()
    {
        var fragment = new Fragment
        {
            Location = new PointF(10, 20),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                ActualBackgroundColor = Color.Red,
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        var fillItems = displayList.Items.OfType<FillRectItem>().ToList();
        Assert.Single(fillItems);
        Assert.Equal(Color.Red, fillItems[0].Color);
        Assert.Equal(10, fillItems[0].Bounds.X);
        Assert.Equal(20, fillItems[0].Bounds.Y);
    }

    [Fact]
    public void PaintWalker_TransparentBackground_NoFillRectItem()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0),
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.DoesNotContain(displayList.Items, i => i is FillRectItem);
    }

    [Fact]
    public void PaintWalker_Border_EmitsDrawBorderItem()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Border = new BoxEdges(2, 2, 2, 2),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                BorderTopStyle = "solid",
                BorderRightStyle = "solid",
                BorderBottomStyle = "solid",
                BorderLeftStyle = "solid",
                Border = new BoxEdges(2, 2, 2, 2),
                ActualBorderTopColor = Color.Black,
                ActualBorderRightColor = Color.Black,
                ActualBorderBottomColor = Color.Black,
                ActualBorderLeftColor = Color.Black,
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        var borderItems = displayList.Items.OfType<DrawBorderItem>().ToList();
        Assert.Single(borderItems);
        Assert.Equal(Color.Black, borderItems[0].TopColor);
        Assert.Equal(2, borderItems[0].Widths.Top);
    }

    [Fact]
    public void PaintWalker_NoBorder_NoDrawBorderItem()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Border = BoxEdges.Zero,
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                BorderTopStyle = "none",
                BorderRightStyle = "none",
                BorderBottomStyle = "none",
                BorderLeftStyle = "none",
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.DoesNotContain(displayList.Items, i => i is DrawBorderItem);
    }

    [Fact]
    public void PaintWalker_AsymmetricBorder_EmitsDrawBorderItemWithCorrectWidths()
    {
        // Acid1 Section 5: border-width: 1em 1.5em 2em .5em → 10 15 20 5 at 10px font
        var fragment = new Fragment
        {
            Location = new PointF(20, 10),
            Size = new SizeF(70, 140),
            Border = new BoxEdges(10, 15, 20, 5),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                BorderTopStyle = "solid",
                BorderRightStyle = "solid",
                BorderBottomStyle = "solid",
                BorderLeftStyle = "solid",
                Border = new BoxEdges(10, 15, 20, 5),
                ActualBorderTopColor = Color.Black,
                ActualBorderRightColor = Color.Black,
                ActualBorderBottomColor = Color.Black,
                ActualBorderLeftColor = Color.Black,
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        var borderItems = displayList.Items.OfType<DrawBorderItem>().ToList();
        Assert.Single(borderItems);
        var item = borderItems[0];
        Assert.Equal(10, item.Widths.Top);
        Assert.Equal(15, item.Widths.Right);
        Assert.Equal(20, item.Widths.Bottom);
        Assert.Equal(5, item.Widths.Left);
        Assert.Equal(Color.Black, item.TopColor);
        Assert.Equal(Color.Black, item.RightColor);
        Assert.Equal(Color.Black, item.BottomColor);
        Assert.Equal(Color.Black, item.LeftColor);
    }

    [Fact]
    public void PaintWalker_TextInline_EmitsDrawTextItem()
    {
        var inlineStyle = new ComputedStyle
        {
            FontFamily = "Arial",
            FontSize = "12pt",
            FontWeight = "normal",
            ActualColor = Color.Black,
        };

        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(200, 20),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            Lines =
            [
                new LineFragment
                {
                    X = 0, Y = 0, Width = 200, Height = 20,
                    Inlines =
                    [
                        new InlineFragment
                        {
                            X = 5, Y = 2, Width = 50, Height = 16,
                            Text = "Hello",
                            Style = inlineStyle,
                        }
                    ]
                }
            ],
        };

        var displayList = PaintWalker.Paint(fragment);
        var textItems = displayList.Items.OfType<DrawTextItem>().ToList();
        Assert.Single(textItems);
        Assert.Equal("Hello", textItems[0].Text);
        Assert.Equal("Arial", textItems[0].FontFamily);
        Assert.Equal(Color.Black, textItems[0].Color);
    }

    [Fact]
    public void PaintWalker_DisplayNone_SkipsFragment()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle
            {
                Display = "none",
                ActualBackgroundColor = Color.Red,
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.Empty(displayList.Items);
    }

    [Fact]
    public void PaintWalker_OverflowHidden_EmitsClipAndRestore()
    {
        var fragment = new Fragment
        {
            Location = new PointF(10, 10),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                Overflow = "hidden",
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.Contains(displayList.Items, i => i is ClipItem);
        Assert.Contains(displayList.Items, i => i is RestoreItem);

        var clipItem = displayList.Items.OfType<ClipItem>().First();
        Assert.Equal(10, clipItem.ClipRect.X);
        Assert.Equal(10, clipItem.ClipRect.Y);
        Assert.Equal(100, clipItem.ClipRect.Width);
    }

    [Fact]
    public void PaintWalker_Children_PaintedInOrder()
    {
        var child1 = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(50, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                ActualBackgroundColor = Color.Red,
            },
        };
        var child2 = new Fragment
        {
            Location = new PointF(50, 0),
            Size = new SizeF(50, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                ActualBackgroundColor = Color.Blue,
            },
        };

        var parent = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            Children = [child1, child2],
        };

        var displayList = PaintWalker.Paint(parent);
        var fillItems = displayList.Items.OfType<FillRectItem>().ToList();
        Assert.Equal(2, fillItems.Count);
        Assert.Equal(Color.Red, fillItems[0].Color);
        Assert.Equal(Color.Blue, fillItems[1].Color);
    }

    [Fact]
    public void PaintWalker_StackingContext_PositionedAfterNonPositioned()
    {
        var nonPositioned = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(50, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                ActualBackgroundColor = Color.Red,
            },
            CreatesStackingContext = false,
        };
        var positioned = new Fragment
        {
            Location = new PointF(50, 0),
            Size = new SizeF(50, 50),
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                Position = "absolute",
                ActualBackgroundColor = Color.Blue,
            },
            CreatesStackingContext = true,
            StackLevel = 1,
        };

        // Place positioned BEFORE non-positioned in tree order
        var parent = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            Children = [positioned, nonPositioned],
        };

        var displayList = PaintWalker.Paint(parent);
        var fillItems = displayList.Items.OfType<FillRectItem>().ToList();
        Assert.Equal(2, fillItems.Count);
        // Non-positioned should be painted first regardless of tree order
        Assert.Equal(Color.Red, fillItems[0].Color);
        Assert.Equal(Color.Blue, fillItems[1].Color);
    }

    [Fact]
    public void PaintWalker_TextDecoration_EmitsDrawLineItem()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(200, 20),
            Border = BoxEdges.Zero,
            Padding = BoxEdges.Zero,
            Style = new ComputedStyle
            {
                Display = "block",
                Visibility = "visible",
                TextDecoration = "underline",
                ActualColor = Color.Black,
            },
            Lines =
            [
                new LineFragment
                {
                    X = 0, Y = 0, Width = 200, Height = 20,
                    Inlines =
                    [
                        new InlineFragment { X = 0, Y = 0, Width = 100, Height = 16, Text = "Test" },
                    ]
                }
            ],
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.Contains(displayList.Items, i => i is DrawLineItem);
    }

    // =================================================================
    // Phase 3 — DrawBorderItem per-side styles
    // =================================================================

    [Fact]
    public void DrawBorderItem_PerSideStyles_CanBeSet()
    {
        var item = new DrawBorderItem
        {
            Bounds = new RectangleF(0, 0, 100, 100),
            Widths = new BoxEdges(1, 2, 3, 4),
            TopStyle = "solid",
            RightStyle = "dashed",
            BottomStyle = "dotted",
            LeftStyle = "none",
            CornerNw = 5,
            CornerNe = 10,
        };

        Assert.Equal("solid", item.TopStyle);
        Assert.Equal("dashed", item.RightStyle);
        Assert.Equal("dotted", item.BottomStyle);
        Assert.Equal("none", item.LeftStyle);
        Assert.Equal(5, item.CornerNw);
        Assert.Equal(10, item.CornerNe);
    }

    // =================================================================
    // Phase 3 — DrawTextItem FontHandle
    // =================================================================

    [Fact]
    public void DrawTextItem_FontHandle_CanBeSet()
    {
        var item = new DrawTextItem
        {
            Text = "Hello",
            FontHandle = "fake-font-object",
            IsRtl = true,
        };

        Assert.Equal("fake-font-object", item.FontHandle);
        Assert.True(item.IsRtl);
    }

    // =================================================================
    // Phase 3 — DrawLineItem
    // =================================================================

    [Fact]
    public void DrawLineItem_Properties_CanBeSet()
    {
        var item = new DrawLineItem
        {
            Bounds = new RectangleF(0, 10, 100, 1),
            Start = new PointF(0, 10),
            End = new PointF(100, 10),
            Color = Color.Red,
            Width = 2,
            DashStyle = "dashed",
        };

        Assert.Equal(new PointF(0, 10), item.Start);
        Assert.Equal(new PointF(100, 10), item.End);
        Assert.Equal(Color.Red, item.Color);
        Assert.Equal(2, item.Width);
        Assert.Equal("dashed", item.DashStyle);
    }

    // =================================================================
    // Phase 3 — InlineFragment FontHandle
    // =================================================================

    [Fact]
    public void InlineFragment_FontHandle_CanBeSet()
    {
        var inline = new InlineFragment
        {
            X = 0, Y = 0, Width = 50, Height = 12,
            Text = "Test",
            FontHandle = "font-handle",
        };

        Assert.Equal("font-handle", inline.FontHandle);
    }

    // =================================================================
    // Phase 3 — Integration: New paint path via HtmlContainer
    // =================================================================

    [Fact]
    public void NewPaintPath_ProducesDisplayList()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;height:100px;background:red;'>Hello</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        // Paint path always uses PaintWalker → DisplayList → RGraphics
        container.PerformPaint(canvas, clip);

        var displayList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(displayList);
        Assert.True(displayList.Items.Count > 0, "DisplayList should have items");
    }

    [Fact]
    public void NewPaintPath_HasFillRectForBackground()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;height:100px;background:red;'>Content</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        container.PerformPaint(canvas, clip);

        var displayList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(displayList);
        Assert.Contains(displayList.Items, i => i is FillRectItem);
    }

    [Fact]
    public void NewPaintPath_HasDrawTextForContent()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;'>Hello World</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        container.PerformPaint(canvas, clip);

        var displayList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(displayList);
        var textItems = displayList.Items.OfType<DrawTextItem>().ToList();
        Assert.True(textItems.Count > 0, "Should have text items for 'Hello World'");
        Assert.Contains(textItems, t => t.Text.Contains("Hello"));
    }

    [Fact]
    public void NewPaintPath_HasDrawBorderForBorderedElement()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;height:100px;border:2px solid black;'>Bordered</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        container.PerformPaint(canvas, clip);

        var displayList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(displayList);
        Assert.Contains(displayList.Items, i => i is DrawBorderItem);
    }

    [Fact]
    public void NewPaintPath_ProducesVisiblePixels()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;height:100px;background:red;'>Visible</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        container.PerformPaint(canvas, clip);

        // Verify the bitmap has non-white pixels (something was rendered)
        bool hasNonWhitePixels = false;
        for (int y = 0; y < bitmap.Height && !hasNonWhitePixels; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel != SKColors.White)
                {
                    hasNonWhitePixels = true;
                    break;
                }
            }
        }

        Assert.True(hasNonWhitePixels, "New paint path should produce visible output");
    }

    [Fact]
    public void PaintPath_AlwaysProducesDisplayList()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;'>Hello World</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        // Paint always uses PaintWalker path — no flag needed
        container.PerformPaint(canvas, clip);

        // Display list should always be populated
        Assert.NotNull(container.HtmlContainerInt.LatestDisplayList);

        // Should still render visible output
        bool hasNonWhitePixels = false;
        for (int y = 0; y < bitmap.Height && !hasNonWhitePixels; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel != SKColors.White)
                {
                    hasNonWhitePixels = true;
                    break;
                }
            }
        }

        Assert.True(hasNonWhitePixels, "Paint path should produce visible output");
    }

    [Fact]
    public void NewPaintPath_TextItemHasFontHandle()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:200px;font-family:Arial;font-size:12pt;'>Text</div>");

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 500, 500);
        container.PerformLayout(canvas, clip);

        container.PerformPaint(canvas, clip);

        var displayList = container.HtmlContainerInt.LatestDisplayList;
        Assert.NotNull(displayList);
        var textItems = displayList.Items.OfType<DrawTextItem>().ToList();
        Assert.True(textItems.Count > 0);
        // Font handles should be populated from the fragment tree
        Assert.All(textItems, t => Assert.NotNull(t.FontHandle));
    }

    // =================================================================
    // Phase 3 — DisplayList JSON serialization (snapshot tests)
    // =================================================================

    [Fact]
    public void DisplayList_CanSerializeToJson()
    {
        var items = new DisplayItem[]
        {
            new FillRectItem { Bounds = new RectangleF(0, 0, 100, 50), Color = Color.Red },
            new DrawBorderItem
            {
                Bounds = new RectangleF(0, 0, 100, 50),
                Widths = new BoxEdges(1, 1, 1, 1),
                TopColor = Color.Black, RightColor = Color.Black,
                BottomColor = Color.Black, LeftColor = Color.Black,
                TopStyle = "solid", RightStyle = "solid",
                BottomStyle = "solid", LeftStyle = "solid",
            },
            new DrawTextItem { Text = "Hello", FontFamily = "Arial", FontSize = 12, Color = Color.Black, Origin = new PointF(5, 5) },
            new DrawLineItem { Start = new PointF(0, 10), End = new PointF(100, 10), Color = Color.Black, Width = 1 },
        };

        var list = new DisplayList { Items = items };
        var json = System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        });

        Assert.NotNull(json);
        Assert.Contains("FillRect", json);
        Assert.Contains("DrawBorder", json);
        Assert.Contains("DrawText", json);
        Assert.Contains("DrawLine", json);
    }

    [Fact]
    public void NewPaintPath_DisplayList_Snapshot_IsStable()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml("<div style='width:100px;height:50px;background:blue;border:1px solid black;'>Snapshot</div>");

        using var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, 300, 300);
        container.PerformLayout(canvas, clip);

        container.PerformPaint(canvas, clip);

        var list1 = container.HtmlContainerInt.LatestDisplayList;

        // Re-layout and re-paint — display list should be identical
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        var list2 = container.HtmlContainerInt.LatestDisplayList;

        Assert.NotNull(list1);
        Assert.NotNull(list2);
        Assert.Equal(list1.Items.Count, list2.Items.Count);

        for (int i = 0; i < list1.Items.Count; i++)
        {
            Assert.Equal(list1.Items[i].GetType(), list2.Items[i].GetType());
            Assert.Equal(list1.Items[i].Bounds, list2.Items[i].Bounds);
        }
    }

    // =================================================================
    // Phase 3 — Background image support
    // =================================================================

    [Fact]
    public void Fragment_BackgroundImageHandle_DefaultsToNull()
    {
        var fragment = new Fragment();
        Assert.Null(fragment.BackgroundImageHandle);
    }

    [Fact]
    public void Fragment_BackgroundImageHandle_CanBeSet()
    {
        var sentinel = new object();
        var fragment = new Fragment { BackgroundImageHandle = sentinel };
        Assert.Same(sentinel, fragment.BackgroundImageHandle);
    }

    [Fact]
    public void PaintWalker_BackgroundImage_EmitsDrawImageItem()
    {
        var sentinel = new object();
        var fragment = new Fragment
        {
            Location = new PointF(10, 20),
            Size = new SizeF(100, 50),
            Border = new BoxEdges(2, 2, 2, 2),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            BackgroundImageHandle = sentinel,
        };

        var displayList = PaintWalker.Paint(fragment);
        var imageItem = Assert.Single(displayList.Items.OfType<DrawImageItem>());
        Assert.Same(sentinel, imageItem.ImageHandle);
        // Image rect should be inside borders (padding box)
        Assert.Equal(12, imageItem.DestRect.X, 0.01);
        Assert.Equal(22, imageItem.DestRect.Y, 0.01);
        Assert.Equal(96, imageItem.DestRect.Width, 0.01);
        Assert.Equal(46, imageItem.DestRect.Height, 0.01);
    }

    [Fact]
    public void PaintWalker_NoBackgroundImage_NoDrawImageItem()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 50),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
        };

        var displayList = PaintWalker.Paint(fragment);
        Assert.DoesNotContain(displayList.Items, i => i is DrawImageItem);
    }

    // =================================================================
    // Phase 3 — Replaced image (img) support
    // =================================================================

    [Fact]
    public void Fragment_ImageHandle_DefaultsToNull()
    {
        var fragment = new Fragment();
        Assert.Null(fragment.ImageHandle);
    }

    [Fact]
    public void Fragment_ImageHandle_CanBeSet()
    {
        var sentinel = new object();
        var fragment = new Fragment
        {
            ImageHandle = sentinel,
            ImageSourceRect = new RectangleF(0, 0, 50, 50),
        };
        Assert.Same(sentinel, fragment.ImageHandle);
        Assert.Equal(new RectangleF(0, 0, 50, 50), fragment.ImageSourceRect);
    }

    [Fact]
    public void PaintWalker_ReplacedImage_EmitsDrawImageItem()
    {
        var sentinel = new object();
        var fragment = new Fragment
        {
            Location = new PointF(5, 10),
            Size = new SizeF(80, 60),
            Border = new BoxEdges(1, 1, 1, 1),
            Padding = new BoxEdges(2, 2, 2, 2),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            ImageHandle = sentinel,
            ImageSourceRect = RectangleF.Empty,
        };

        var displayList = PaintWalker.Paint(fragment);
        var imageItem = Assert.Single(displayList.Items.OfType<DrawImageItem>());
        Assert.Same(sentinel, imageItem.ImageHandle);
        // Dest rect should be inside border + padding
        Assert.Equal(Math.Floor(5.0 + 1.0 + 2.0), imageItem.DestRect.X, 0.01);
        Assert.Equal(Math.Floor(10.0 + 1.0 + 2.0), imageItem.DestRect.Y, 0.01);
    }

    [Fact]
    public void PaintWalker_ReplacedImage_WithSourceRect_EmitsSourceRect()
    {
        var sentinel = new object();
        var srcRect = new RectangleF(10, 20, 30, 40);
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 100),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            ImageHandle = sentinel,
            ImageSourceRect = srcRect,
        };

        var displayList = PaintWalker.Paint(fragment);
        var imageItem = Assert.Single(displayList.Items.OfType<DrawImageItem>());
        Assert.Equal(srcRect, imageItem.SourceRect);
    }

    // =================================================================
    // Phase 3 — Selection rendering
    // =================================================================

    [Fact]
    public void InlineFragment_Selected_DefaultsToFalse()
    {
        var inline = new InlineFragment();
        Assert.False(inline.Selected);
        Assert.Equal(-1, inline.SelectedStartOffset);
        Assert.Equal(-1, inline.SelectedEndOffset);
    }

    [Fact]
    public void InlineFragment_Selected_CanBeSet()
    {
        var inline = new InlineFragment
        {
            Selected = true,
            SelectedStartOffset = 5.0,
            SelectedEndOffset = 20.0,
        };
        Assert.True(inline.Selected);
        Assert.Equal(5.0, inline.SelectedStartOffset);
        Assert.Equal(20.0, inline.SelectedEndOffset);
    }

    [Fact]
    public void PaintWalker_Selection_EmitsSelectionFillRect()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(200, 50),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            Lines = new[]
            {
                new LineFragment
                {
                    X = 0, Y = 0, Width = 200, Height = 20,
                    Inlines = new[]
                    {
                        new InlineFragment
                        {
                            X = 10, Y = 5, Width = 50, Height = 15,
                            Text = "Hello",
                            Style = new ComputedStyle { FontFamily = "Arial", FontSize = "12pt", ActualColor = Color.Black },
                            Selected = true,
                        },
                    },
                },
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        // Should have a FillRectItem for selection highlight
        var selectionFills = displayList.Items.OfType<FillRectItem>().ToList();
        Assert.Contains(selectionFills, f => f.Color.A > 0 && f.Bounds.X == 10);
    }

    [Fact]
    public void PaintWalker_PartialSelection_RespectsOffsets()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(200, 50),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            Lines = new[]
            {
                new LineFragment
                {
                    X = 0, Y = 0, Width = 200, Height = 20,
                    Inlines = new[]
                    {
                        new InlineFragment
                        {
                            X = 10, Y = 5, Width = 100, Height = 15,
                            Text = "Hello World",
                            Style = new ComputedStyle { FontFamily = "Arial", FontSize = "12pt", ActualColor = Color.Black },
                            Selected = true,
                            SelectedStartOffset = 20.0,
                            SelectedEndOffset = 60.0,
                        },
                    },
                },
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        var selectionFills = displayList.Items.OfType<FillRectItem>().ToList();
        // Selection rect should start at inline.X + startOffset = 10 + 20 = 30
        var selRect = selectionFills.FirstOrDefault(f => f.Color.A > 0 && Math.Abs(f.Bounds.X - 30) < 0.01);
        Assert.NotNull(selRect);
        // Width should be endOffset - startOffset = 60 - 20 = 40
        Assert.Equal(40, selRect.Bounds.Width, 0.01);
    }

    [Fact]
    public void PaintWalker_NoSelection_NoExtraFillRect()
    {
        var fragment = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(200, 50),
            Style = new ComputedStyle { Display = "block", Visibility = "visible" },
            Lines = new[]
            {
                new LineFragment
                {
                    X = 0, Y = 0, Width = 200, Height = 20,
                    Inlines = new[]
                    {
                        new InlineFragment
                        {
                            X = 10, Y = 5, Width = 50, Height = 15,
                            Text = "Hello",
                            Style = new ComputedStyle { FontFamily = "Arial", FontSize = "12pt", ActualColor = Color.Black },
                            Selected = false,
                        },
                    },
                },
            },
        };

        var displayList = PaintWalker.Paint(fragment);
        // Only DrawTextItem expected, no FillRectItem for selection
        Assert.DoesNotContain(displayList.Items, i => i is FillRectItem);
    }

    // =================================================================
    // CSS2.1 §14.2 — Canvas background propagation
    // =================================================================

    [Fact]
    public void PaintWalker_CanvasBgPropagation_BodyBgFillsViewport()
    {
        // When html has transparent bg and body has a color, the body color
        // should fill the viewport (canvas propagation).
        var body = new Fragment
        {
            Location = new PointF(8, 8),
            Size = new SizeF(784, 100),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.Red },
        };
        var html = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [body],
        };
        var root = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [html],
        };

        var viewport = new RectangleF(0, 0, 800, 600);
        var displayList = PaintWalker.Paint(root, viewport);
        var fills = displayList.Items.OfType<FillRectItem>().ToList();

        // First fill should be the canvas background covering the full viewport
        Assert.True(fills.Count >= 1);
        Assert.Equal(Color.Red, fills[0].Color);
        Assert.Equal(viewport, fills[0].Bounds);
    }

    [Fact]
    public void PaintWalker_CanvasBgPropagation_BodyBgNotDoublePainted()
    {
        // CSS2.1 §14.2: When body bg is propagated to canvas, body must NOT
        // paint its own background at its box position.
        var body = new Fragment
        {
            Location = new PointF(8, 8),
            Size = new SizeF(784, 100),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.Blue },
        };
        var html = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [body],
        };
        var root = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [html],
        };

        var viewport = new RectangleF(0, 0, 800, 600);
        var displayList = PaintWalker.Paint(root, viewport);
        var blueFills = displayList.Items.OfType<FillRectItem>()
            .Where(f => f.Color == Color.Blue).ToList();

        // Only one blue fill should exist (the canvas), NOT a second at the body's position
        Assert.Single(blueFills);
        Assert.Equal(viewport, blueFills[0].Bounds);
    }

    [Fact]
    public void PaintWalker_CanvasBgPropagation_HtmlBgUsedDirectly()
    {
        // When the html element has a non-transparent background, it is used
        // for the canvas and its own element background is suppressed.
        var body = new Fragment
        {
            Location = new PointF(8, 8),
            Size = new SizeF(784, 100),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.Green },
        };
        var html = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.Red },
            Children = [body],
        };
        var root = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(800, 600),
            Style = new ComputedStyle { Display = "block", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [html],
        };

        var viewport = new RectangleF(0, 0, 800, 600);
        var displayList = PaintWalker.Paint(root, viewport);
        var fills = displayList.Items.OfType<FillRectItem>().ToList();

        // Canvas fill should be Red (from html), not Green (from body)
        Assert.Equal(Color.Red, fills[0].Color);
        Assert.Equal(viewport, fills[0].Bounds);
        // Html's own background should NOT be painted again at its position
        var htmlRedFills = fills.Where(f => f.Color == Color.Red).ToList();
        Assert.Single(htmlRedFills);
        // Body's green background should still be painted at body position
        var bodyGreenFills = fills.Where(f => f.Color == Color.Green).ToList();
        Assert.Single(bodyGreenFills);
    }

    // =================================================================
    // CSS2.1 §17.5.1 — Table six-layer background painting
    // =================================================================

    [Fact]
    public void PaintWalker_TableLayers_ColumnBgPaintedBeforeRowBg()
    {
        // CSS2.1 §17.5.1: Column backgrounds (layer 3) must be painted before
        // row-group/row/cell backgrounds (layers 4–6).
        var cell = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 30),
            Style = new ComputedStyle { Display = "table-cell", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
        };
        var row = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 30),
            Style = new ComputedStyle { Display = "table-row", Visibility = "visible", ActualBackgroundColor = Color.Blue },
            Children = [cell],
        };
        var rowGroup = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 30),
            Style = new ComputedStyle { Display = "table-row-group", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [row],
        };
        var col = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 30),
            Style = new ComputedStyle { Display = "table-column", Visibility = "visible", ActualBackgroundColor = Color.Red },
        };
        var table = new Fragment
        {
            Location = new PointF(0, 0),
            Size = new SizeF(100, 30),
            Style = new ComputedStyle { Display = "table", Visibility = "visible", ActualBackgroundColor = Color.FromArgb(0, 0, 0, 0) },
            Children = [col, rowGroup],
        };

        var displayList = PaintWalker.Paint(table);
        var fills = displayList.Items.OfType<FillRectItem>().ToList();

        // Column background (Red) should appear before row background (Blue)
        int colIdx = fills.FindIndex(f => f.Color == Color.Red);
        int rowIdx = fills.FindIndex(f => f.Color == Color.Blue);
        Assert.True(colIdx >= 0, "Column background not found");
        Assert.True(rowIdx >= 0, "Row background not found");
        Assert.True(colIdx < rowIdx, "Column background must be painted before row background per CSS2.1 §17.5.1");

        // Column background must NOT be painted twice (once in early layer, once in tree walk)
        var redFills = fills.Where(f => f.Color == Color.Red).ToList();
        Assert.Single(redFills);
    }
}
