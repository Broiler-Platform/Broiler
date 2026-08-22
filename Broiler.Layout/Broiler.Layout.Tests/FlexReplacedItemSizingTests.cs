using System;
using System.Collections.Generic;
using System.Drawing;
using Broiler.CSS;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS Flexbox §4.5 for a <em>replaced</em> flex item: the size suggestions that decide how far an
/// <c>&lt;img&gt;</c> in a flex row is allowed to shrink.
/// </summary>
/// <remarks>
/// <para>
/// Two of them were wrong. The <em>content size suggestion</em> came from a measurement of the box's
/// content, which for a replaced box is the single word carrying its <em>used</em> size — so an
/// <c>&lt;img style="width:999px"&gt;</c> reported a 999px min-content size and floored the item
/// there, however much the container asked it to shrink. And there was no <em>transferred size
/// suggestion</em> at all, so an image stretched to a short flex line was floored at its bitmap's
/// width rather than at the width its aspect ratio asks for at that height.
/// </para>
/// <para>
/// The numbers below are Chromium's, taken on the same markup: WPT
/// <c>css-flexbox/flex-minimum-width-flex-items-013</c> is the first case and states the reasoning in
/// its own comment (a 300×150 image stretched to a 50px-tall line has a min-content width of 100px,
/// not 300px), and <c>-010</c> and <c>-012</c> are the <c>max-height</c> ones.
/// </para>
/// <para>
/// The tree is built with the image already <c>display: block</c> under the flex container, which is
/// what blockification leaves a flex item as — see
/// <see cref="FlexGridItemBlockification.IsRowFlexItem"/> for the box fix-up that must then leave it
/// unwrapped, without which none of these sizes reach the image at all.
/// </para>
/// </remarks>
public sealed class FlexReplacedItemSizingTests
{
    private static readonly Uri BaseUrl = new("file:///flex.html");

    private const double NaturalWidth = 300;
    private const double NaturalHeight = 150;
    private const float PageWidth = 400;

    /// <summary>
    /// The §4.5 shape the WPT test states: a zero-width row 50px tall, so the item's cross size is
    /// definite at 50 and the ratio transfers it to a 100px main-axis floor — the declared 999px
    /// neither floors it nor survives the shrink.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Stretched_Image_Shrinks_To_Its_Transferred_Size_Suggestion()
    {
        var (root, image) = FlexRowWithAnImage(
            containerWidth: "0px", containerHeight: "50px", imageStyle: box => box.Width = "999px");

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, image.Size.Width, 3);
    }

    /// <summary>The same row with no declared width on the image: the content size suggestion is
    /// still bounded by the transferred one, so it shrinks to 100 rather than to the bitmap's 300.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Undeclared_Image_Shrinks_To_Its_Transferred_Size_Suggestion()
    {
        var (root, image) = FlexRowWithAnImage(
            containerWidth: "0px", containerHeight: "50px", imageStyle: null);

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, image.Size.Width, 3);
    }

    /// <summary>
    /// With the container's cross size content-based there is nothing to transfer, so the floor is
    /// the content size suggestion alone — the bitmap's natural width, not the declared 999px. This
    /// is the case that shows the suggestion is read from the natural size: before, the item stayed
    /// at 999.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Without_A_Definite_Cross_Size_The_Floor_Is_The_Natural_Width()
    {
        var (root, image) = FlexRowWithAnImage(
            containerWidth: "0px", containerHeight: null, imageStyle: box => box.Width = "999px");

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(NaturalWidth, image.Size.Width, 3);
    }

    /// <summary>
    /// <c>max-height</c> bounds the cross size the ratio transfers, which is what
    /// <c>flex-minimum-width-flex-items-012</c> asserts: the item may shrink to 100 even though its
    /// own <c>height</c> says 2000.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Max_Height_Bounds_The_Cross_Size_That_Transfers()
    {
        var (root, image) = FlexRowWithAnImage(
            containerWidth: "0px",
            containerHeight: null,
            imageStyle: box =>
            {
                box.Height = "2000px";
                box.MaxHeight = "50px";
            });

        root.PerformLayout(root.LayoutEnvironment);

        Assert.Equal(100, image.Size.Width, 3);
    }

    /// <summary>
    /// <c>min-width: 0</c> is the one spelling that always lets a flex item collapse — §4.5's
    /// automatic minimum does not apply at all — and a replaced item must obey it like any other.
    /// It could not: the item stayed at its declared 999px.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Explicit_Zero_Min_Width_Still_Collapses_A_Replaced_Item()
    {
        var (root, image) = FlexRowWithAnImage(
            containerWidth: "40px",
            containerHeight: null,
            imageStyle: box =>
            {
                box.Width = "999px";
                box.MinWidth = "0";
                box.IsMinWidthSpecified = true;
            });

        root.PerformLayout(root.LayoutEnvironment);

        // Not 999 (the declared width, which used to be the floor), and not the 300 the automatic
        // minimum would give it: `min-width: 0` opts out of §4.5 entirely, so the item takes the
        // room the container has.
        Assert.Equal(40, image.Size.Width, 3);
    }

    /// <summary>The control: a non-replaced item has no natural size and no ratio, so neither
    /// suggestion applies and it still floors at the min-content width of its own contents.</summary>
    [Fact(Timeout = 600000)]
    public void A_Non_Replaced_Item_Is_Unaffected()
    {
        var environment = new StubLayoutEnvironment();
        var (root, container) = FlexRow(environment, containerWidth: "0px", containerHeight: "50px");

        var item = new CssBox(container, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = CssConstants.Block,
            FontSize = "16px",
            Width = "999px",
        };

        root.PerformLayout(environment);

        Assert.True(item.Size.Width < 999, $"a shrinkable item should not keep its declared width ({item.Size.Width})");
    }

    // ───────────────────────── the predicate the box fix-up asks ─────────────────────────

    [Fact(Timeout = 600000)]
    public void An_Image_In_A_Row_Flex_Container_Is_A_Row_Flex_Item()
    {
        var (_, image) = FlexRowWithAnImage("0px", "50px", imageStyle: null);

        Assert.True(FlexGridItemBlockification.IsRowFlexItem(image));
    }

    /// <summary>A column container places its items by ordinary block flow, which cannot position a
    /// block-level replaced box — so it keeps the anonymous wrapper and the predicate must say so.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Image_In_A_Column_Flex_Container_Is_Not()
    {
        var (_, image) = FlexRowWithAnImage("0px", "50px", imageStyle: null, flexDirection: "column");

        Assert.False(FlexGridItemBlockification.IsRowFlexItem(image));
    }

    [Fact(Timeout = 600000)]
    public void An_Image_In_A_Block_Is_Not()
    {
        var (_, image) = FlexRowWithAnImage("0px", "50px", imageStyle: null, containerDisplay: CssConstants.Block);

        Assert.False(FlexGridItemBlockification.IsRowFlexItem(image));
    }

    [Fact(Timeout = 600000)]
    public void An_Absolutely_Positioned_Image_Is_Not_An_Item()
    {
        var (_, image) = FlexRowWithAnImage(
            "0px", "50px", imageStyle: box => box.Position = CssConstants.Absolute);

        Assert.False(FlexGridItemBlockification.IsRowFlexItem(image));
    }

    // ───────────────────────────────── the shapes ─────────────────────────────────

    private static (CssBox Root, CssBoxImage Image) FlexRowWithAnImage(
        string containerWidth,
        string? containerHeight,
        Action<CssBoxImage>? imageStyle,
        string flexDirection = "row",
        string containerDisplay = "flex")
    {
        var environment = new StubLayoutEnvironment();
        var (root, container) = FlexRow(environment, containerWidth, containerHeight, flexDirection, containerDisplay);

        var image = new CssBoxImage(
            container,
            new HtmlTag("img", true, new Dictionary<string, string> { ["src"] = "i.png" }),
            BaseUrl)
        {
            // What blockification leaves a flex item as, and what the box fix-up must now leave
            // unwrapped.
            Display = CssConstants.Block,
            FontSize = "16px",
        };

        imageStyle?.Invoke(image);

        return (root, image);
    }

    private static (CssBox Root, CssBox Container) FlexRow(
        StubLayoutEnvironment environment,
        string containerWidth,
        string? containerHeight,
        string flexDirection = "row",
        string containerDisplay = "flex")
    {
        var root = new CssBox(null, null, BaseUrl)
        {
            Location = PointF.Empty,
            Size = new SizeF(PageWidth, PageWidth),
            Display = CssConstants.Block,
            FontSize = "16px",
            LayoutEnvironment = environment,
        };
        var html = Block(root);
        var body = Block(html);

        var container = new CssBox(body, new HtmlTag("div", false, null), BaseUrl)
        {
            Display = containerDisplay,
            FlexDirection = flexDirection,
            FontSize = "16px",
            Width = containerWidth,
        };

        if (containerHeight != null)
            container.Height = containerHeight;

        return (root, container);
    }

    private static CssBox Block(CssBox parent) => new(parent, null, BaseUrl)
    {
        Location = PointF.Empty,
        Size = new SizeF(PageWidth, 0),
        Display = CssConstants.Block,
        FontSize = "16px",
    };

    private sealed class StubImageLoader(Action<object?, RectangleF, bool> onComplete) : ILayoutImageLoader
    {
        private static readonly object TheImage = new();

        public object? Image { get; private set; }
        public RectangleF Rectangle => RectangleF.Empty;

        public void LoadImage(string src, IReadOnlyDictionary<string, string>? attributes, Uri baseUrl)
        {
            Image = TheImage;
            onComplete(TheImage, RectangleF.Empty, false);
        }

        public void Dispose() { }
    }

    private sealed class StubLayoutEnvironment : ILayoutEnvironment
    {
        public Broiler.Graphics.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => new StubFont(size);
        public SizeF MeasureText(Broiler.Graphics.ILayoutFont font, string text) => SizeF.Empty;
        public void MeasureText(Broiler.Graphics.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = 0; charFitWidth = 0; }
        public double GetWhitespaceWidth(Broiler.Graphics.ILayoutFont font) => 0;
        public ImageIntrinsics GetImageIntrinsics(object imageHandle) => new(NaturalWidth, NaturalHeight, true);
        public Broiler.Graphics.BColor ParseColor(string value) => default;
        public void RequestRefresh(bool relayout) { }
        public SizeF ViewportSize => new(1000, 1000);
        public PointF RootLocation => PointF.Empty;
        public SizeF ActualSize { get; set; }
        public bool AvoidGeometryAntialias => false;
        public SizeF PageSize => new(1000, 1000);
        public int MarginTop => 0;
        public void ReportLayoutError(string message, Exception? exception = null) { }
        public bool AvoidAsyncImagesLoading => true;
        public bool AvoidImagesLateLoading => true;
        public ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => new StubImageLoader(onComplete);
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class StubFont(double size) : Broiler.Graphics.ILayoutFont
    {
        public double Size { get; } = size;
        public double Height => size;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
