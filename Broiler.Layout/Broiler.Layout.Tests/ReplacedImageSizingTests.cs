using System;
using System.Drawing;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// CSS 2.1 §10.4: how a replaced element's used width and height come out of what the author stated
/// and what the image itself is.
/// </summary>
/// <remarks>
/// The intrinsic ratio fills in a dimension left <c>auto</c>; it never overrules one the author
/// stated. A percentage width is a stated width like any other — it is only resolved later, against
/// the containing block — and treating it as if it were <c>auto</c> made
/// <c>&lt;img width="100%" height="50"&gt;</c> come out as tall as it was wide. That is the shape
/// CSS2's own reference documents use to draw a coloured band, so the cost landed on the
/// <em>reference</em> side of 85 reftests in <c>css/CSS2/backgrounds</c> alone, where the test was
/// right all along.
/// </remarks>
public sealed class ReplacedImageSizingTests
{
    private static readonly Uri BaseUrl = new("file:///image.html");

    /// <summary>A 10×10 image (ratio 1) in a 400px-wide containing block.</summary>
    private static CssRectImage Measure(string width, string height, double imageWidth = 10, double imageHeight = 10)
    {
        var environment = new FakeLayoutEnvironment(new ImageIntrinsics(imageWidth, imageHeight, true));

        var containingBlock = new CssBox(null, null, BaseUrl)
        {
            Display = "block",
            Size = new SizeF(400, 400),
            LayoutEnvironment = environment,
        };

        var box = new CssBox(containingBlock, null, BaseUrl) { Display = "inline", Width = width, Height = height };
        var word = new CssRectImage(box) { Image = new object() };

        CssLayoutEngine.MeasureImageSize(environment, word);
        return word;
    }

    [Fact]
    public void A_Percentage_Width_And_A_Stated_Height_Are_Both_Used()
    {
        var word = Measure("100%", "50px");

        Assert.Equal(400, word.Width, 3);
        Assert.Equal(50, word.Height, 3);
    }

    [Fact]
    public void A_Length_Width_And_A_Stated_Height_Are_Both_Used()
    {
        var word = Measure("200px", "20px");

        Assert.Equal(200, word.Width, 3);
        Assert.Equal(20, word.Height, 3);
    }

    // With the height left to the image, the ratio fills it in — from a percentage width as much as
    // from a length one.
    [Theory]
    [InlineData("100%", 400, 200)]
    [InlineData("50%", 200, 100)]
    [InlineData("120px", 120, 60)]
    public void An_Auto_Height_Comes_From_The_Ratio(string width, double expectedWidth, double expectedHeight)
    {
        var word = Measure(width, "auto", imageWidth: 20, imageHeight: 10);

        Assert.Equal(expectedWidth, word.Width, 3);
        Assert.Equal(expectedHeight, word.Height, 3);
    }

    [Fact]
    public void An_Auto_Width_Comes_From_The_Ratio()
    {
        var word = Measure("auto", "30px", imageWidth: 20, imageHeight: 10);

        Assert.Equal(60, word.Width, 3);
        Assert.Equal(30, word.Height, 3);
    }

    [Fact]
    public void Neither_Stated_Is_The_Intrinsic_Size()
    {
        var word = Measure("auto", "auto", imageWidth: 33, imageHeight: 17);

        Assert.Equal(33, word.Width, 3);
        Assert.Equal(17, word.Height, 3);
    }

    // Minimal ILayoutEnvironment: a fixed font, and the one image's intrinsics.
    private sealed class FakeLayoutEnvironment(ImageIntrinsics intrinsics) : Broiler.Layout.ILayoutEnvironment
    {
        private static readonly Broiler.Graphics.ILayoutFont TheFont = new FakeFont();
        public Broiler.Graphics.ILayoutFont GetFont(string family, double size, LayoutFontStyle style, string? fontFeatures = null) => TheFont;
        public SizeF MeasureText(Broiler.Graphics.ILayoutFont font, string text) => SizeF.Empty;
        public void MeasureText(Broiler.Graphics.ILayoutFont font, string text, double maxWidth, out int charFit, out double charFitWidth) { charFit = 0; charFitWidth = 0; }
        public double GetWhitespaceWidth(Broiler.Graphics.ILayoutFont font) => 0;
        public Broiler.Layout.ImageIntrinsics GetImageIntrinsics(object imageHandle) => intrinsics;
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
        public Broiler.Layout.ILayoutImageLoader CreateImageLoader(Action<object?, RectangleF, bool> onComplete) => null!;
        public string FormatListMarker(int number, string style) => string.Empty;
    }

    private sealed class FakeFont : Broiler.Graphics.ILayoutFont
    {
        public double Size => 16;
        public double Height => 16;
        public double UnderlineOffset => 0;
        public double LeftPadding => 0;
        public string? FontFeatures => null;
    }
}
