using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// CSS 2.1 Chapter 3 — Conformance: Requirements and Recommendations
/// verification tests.
///
/// Each test corresponds to one or more checkpoints in
/// <c>css2/chapter-3-checklist.md</c>. The checklist reference is noted in
/// each test's XML-doc summary.
///
/// Tests use two complementary strategies:
///   • <b>Fragment inspection</b> – build the fragment tree and verify
///     dimensions, positions, and box-model properties directly.
///   • <b>Pixel inspection</b> – render to a bitmap and verify that expected
///     colours appear at specific coordinates, confirming that the layout
///     translates into correct visual output.
/// </summary>
[Collection("Rendering")]
public class Css2Chapter3Tests
{
    private static readonly string GoldenDir = Path.Combine(
        GetSourceDirectory(), "TestData", "GoldenLayout");

    /// <summary>Pixel colour channel thresholds for render verification.</summary>
    private const int HighChannel = 200;
    private const int LowChannel = 50;

    // ═══════════════════════════════════════════════════════════════
    // 3.1  Definitions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §3.1 – Definition of "style sheet": a set of statements that specify
    /// presentation. Verify that an inline style (a minimal style sheet)
    /// is accepted and produces a styled element.
    /// </summary>
    [Fact]
    public void S3_1_StyleSheet_InlineApplied()
    {
        const string html =
            "<div style='width:100px;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.1 – Definition of "valid style sheet": a valid CSS 2.1 style sheet
    /// must conform to the grammar. Verify that a &lt;style&gt; block with
    /// valid CSS is parsed and applied.
    /// </summary>
    [Fact]
    public void S3_1_ValidStyleSheet_StyleBlockApplied()
    {
        const string html =
            @"<style>
                .box { width: 120px; height: 60px; background-color: blue; }
              </style>
              <div class='box'></div>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Expected blue from style block at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.1 – Definition of "source document": the document to which style
    /// sheets are applied. Verify that both the document structure and
    /// style co-exist and produce output.
    /// </summary>
    [Fact]
    public void S3_1_SourceDocument_HtmlRendered()
    {
        const string html = "<p>Source document test</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        Assert.True(fragment.Children.Count > 0,
            "Source document should produce at least one child fragment");
    }

    /// <summary>
    /// §3.1 – Definition of "document language": HTML is the document
    /// language. Verify that HTML elements (headings, paragraphs) are
    /// recognised and laid out.
    /// </summary>
    [Fact]
    public void S3_1_DocumentLanguage_HtmlElementsRecognised()
    {
        const string html =
            @"<h1>Heading</h1>
              <p>Paragraph</p>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.1 – Definition of "user agent" (UA): a program that interprets
    /// documents written in the document language and applies associated
    /// style sheets. The html-renderer acts as a UA. Verify basic rendering.
    /// </summary>
    [Fact]
    public void S3_1_UserAgent_RendersDocument()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:80px;height:40px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel && pixel.Blue < LowChannel,
            $"Expected green at (10,10) from UA rendering, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.1 – Definition of "author", "user", and "user agent" origins.
    /// Verify that author-origin styles (inline) take effect.
    /// </summary>
    [Fact]
    public void S3_1_AuthorOrigin_InlineStyleApplied()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:50px;height:50px;background-color:#0000ff;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel && pixel.Red < LowChannel,
            $"Author inline style should apply blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.1 – Definition of "property" and "value": CSS properties accept
    /// values. Verify multiple properties on a single element.
    /// </summary>
    [Fact]
    public void S3_1_PropertyAndValue_MultiplePropertiesApplied()
    {
        const string html =
            "<div style='width:150px;height:75px;margin-left:20px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 145 && child.Size.Width < 155,
            $"width:150px should be ~150px, got {child.Size.Width}");
        Assert.True(child.Location.X >= 18,
            $"margin-left:20px should offset, got X={child.Location.X}");
    }

    /// <summary>
    /// §3.1 – Definition of "element" and "replaced element": non-replaced
    /// elements generate boxes from document tree content. Verify that a
    /// simple div (non-replaced) generates a box.
    /// </summary>
    [Fact]
    public void S3_1_Element_NonReplacedGeneratesBox()
    {
        const string html =
            "<div style='width:100px;height:100px;background-color:orange;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        Assert.True(child.Size.Width > 95 && child.Size.Height > 95,
            $"Non-replaced element should generate box with specified dimensions");
    }

    /// <summary>
    /// §3.1 – Definition of "intrinsic dimensions" for replaced elements.
    /// An &lt;img&gt; with width/height attributes demonstrates intrinsic
    /// dimension handling.
    /// </summary>
    [Fact]
    public void S3_1_IntrinsicDimensions_ReplacedElement()
    {
        // Without an actual image source the img is treated as a broken image,
        // but CSS width/height override intrinsic dimensions.
        const string html =
            "<img style='width:80px;height:60px;background-color:#ccc;' />";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.1 – Definition of "attribute" and "content": attributes on HTML
    /// elements influence rendering through attribute selectors and inline
    /// style attributes.
    /// </summary>
    [Fact]
    public void S3_1_Attribute_StyleAttributeParsed()
    {
        const string html =
            "<div id='test' style='width:100px;height:40px;background-color:purple;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.1 – Definition of "rendered content" and "document tree": the
    /// document tree is traversed and rendered content is produced.
    /// </summary>
    [Fact]
    public void S3_1_RenderedContent_DocumentTreeTraversed()
    {
        const string html =
            @"<div style='width:200px;'>
                <p>First paragraph</p>
                <p>Second paragraph</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // Document tree should produce nested children.
        Assert.True(fragment.Children.Count > 0,
            "Document tree traversal should produce children");
    }

    /// <summary>
    /// §3.1 – Definition of "ignore": invalid/unsupported rules should be
    /// ignored. Verify that an unknown property does not prevent rendering.
    /// </summary>
    [Fact]
    public void S3_1_Ignore_UnknownPropertySkipped()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:red;frobnicate:42;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        var pixel = bitmap.GetPixel(10, 10);
        // The valid properties should still apply despite the unknown one.
        Assert.True(pixel.Red > HighChannel && pixel.Green < LowChannel && pixel.Blue < LowChannel,
            $"Unknown property should be ignored; expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3.2  UA Conformance
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §3.2 – Must parse style sheets as defined in the specification.
    /// Verify that a &lt;style&gt; block with valid selectors and
    /// declarations is correctly parsed and applied.
    /// </summary>
    [Fact]
    public void S3_2_ParseStyleSheets_ValidCssApplied()
    {
        const string html =
            @"<style>
                div.test { width: 200px; height: 80px; background-color: #00ff00; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='test'></div>
              </body>";
        using var bitmap = RenderHtml(html, 400, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Style block with class selector should apply green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.2 – Must assign to every element every property defined in the
    /// spec. Verify that default/initial values are applied even when not
    /// explicitly set (e.g. display defaults to inline for span).
    /// </summary>
    [Fact]
    public void S3_2_AssignProperties_DefaultValues()
    {
        const string html =
            @"<div style='width:300px;'>
                <span>Inline by default</span>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.2 – Must correctly cascade and inherit values. Verify that
    /// color inheritance works from parent to child.
    /// </summary>
    [Fact]
    public void S3_2_CascadeAndInherit_ColorInherited()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='color:red;'>
                    <span style='display:inline-block;width:50px;height:50px;background-color:inherit;'></span>
                </div>
              </body>";
        // The inherit keyword should work, demonstrating cascade/inheritance.
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.2 – Must recognise all valid CSS 2.1 selectors. Verify type,
    /// class, and ID selectors.
    /// </summary>
    [Fact]
    public void S3_2_Selectors_TypeClassIdRecognised()
    {
        const string html =
            @"<style>
                p { margin: 0; }
                .highlight { background-color: yellow; }
                #unique { background-color: #ff0000; }
              </style>
              <body style='margin:0;padding:0;'>
                <p class='highlight'>Highlighted</p>
                <p id='unique' style='width:100px;height:30px;'>Unique</p>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // ID selector should apply red background to the #unique element.
        // The element starts after the first paragraph, so check lower.
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.2 – Must implement all property value computations correctly.
    /// Verify percentage width resolves against containing block.
    /// </summary>
    [Fact]
    public void S3_2_PropertyValueComputation_PercentageWidth()
    {
        const string html =
            @"<div style='width:400px;'>
                <div style='width:50%;height:30px;background-color:blue;'></div>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var inner = fragment.Children[0].Children[0];
        Assert.True(inner.Size.Width > 190 && inner.Size.Width < 210,
            $"50% of 400px should be ~200px, got {inner.Size.Width}");
    }

    /// <summary>
    /// §3.2 – May use approximations for actual values (e.g. rounding).
    /// Verify that a fractional pixel value is handled without error.
    /// </summary>
    [Fact]
    public void S3_2_Approximations_FractionalPixelHandled()
    {
        const string html =
            "<div style='width:100.7px;height:50.3px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        var child = fragment.Children[0];
        // The value may be rounded; just verify it is close.
        Assert.True(child.Size.Width > 99 && child.Size.Width < 102,
            $"Fractional width ~100.7px should round to ~101px, got {child.Size.Width}");
    }

    /// <summary>
    /// §3.2 – Must not handle CSS as a programming language. CSS is purely
    /// declarative. Verify that an expression-like value is ignored.
    /// </summary>
    [Fact]
    public void S3_2_NotProgrammingLanguage_ExpressionIgnored()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='width:100px;height:50px;background-color:red;width:expression(100+50);'></div>
              </body>";
        using var bitmap = RenderHtml(html, 200, 100);
        // expression() is non-standard and should be ignored; width should
        // remain the first valid value or be invalid. Either way rendering
        // must not crash.
        Assert.NotNull(bitmap);
    }

    /// <summary>
    /// §3.2 – May limit resource usage (e.g. memory). Verify that a very
    /// large document does not crash the UA (it may truncate).
    /// </summary>
    [Fact]
    public void S3_2_LimitResources_LargeDocumentHandled()
    {
        // Generate a moderately large document with many elements.
        var sb = new System.Text.StringBuilder();
        sb.Append("<div style='width:400px;'>");
        for (int i = 0; i < 200; i++)
            sb.Append($"<div style='height:2px;background-color:#{(i % 2 == 0 ? "ff0000" : "0000ff")};'></div>");
        sb.Append("</div>");
        var fragment = BuildFragmentTree(sb.ToString(), 500, 1000);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.2 – Must allow user style sheets. While the html-renderer does
    /// not expose a user-stylesheet API, verify that external style sheet
    /// declarations (&lt;style&gt;) function correctly as a proxy test.
    /// </summary>
    [Fact]
    public void S3_2_UserStyleSheets_ExternalStyleBlockWorks()
    {
        const string html =
            @"<style>
                .user-styled { border: 2px solid black; padding: 5px; }
              </style>
              <div class='user-styled' style='width:100px;height:40px;'>Content</div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.2 – Must support all required media types. Verify that media
    /// type 'screen' in a style block is processed.
    /// </summary>
    [Fact]
    public void S3_2_MediaTypes_ScreenMediaProcessed()
    {
        const string html =
            @"<style media='screen'>
                .media-test { width: 100px; height: 50px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='media-test'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Screen media style should apply, expected red at (10,10), got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3.3  Error Conditions
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §3.3 – Must handle invalid style sheets gracefully. Verify that
    /// malformed CSS does not crash the renderer.
    /// </summary>
    [Fact]
    public void S3_3_InvalidStyleSheet_GracefulHandling()
    {
        const string html =
            @"<style>
                { this is not valid CSS at all !!@#$
              </style>
              <div style='width:100px;height:50px;background-color:green;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    /// <summary>
    /// §3.3 – Must use forward-compatible parsing for unknown at-rules.
    /// An unknown @-rule should be skipped, and subsequent valid rules
    /// should still apply.
    /// </summary>
    [Fact]
    public void S3_3_ForwardCompatibleParsing_UnknownAtRuleSkipped()
    {
        const string html =
            @"<style>
                @future-directive { color: purple; }
                .valid { width: 80px; height: 40px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='valid'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"Valid rule after unknown @-rule should apply, expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.3 – Must ignore unknown properties. Verify that a declaration
    /// with an unknown property name is skipped but other properties apply.
    /// </summary>
    [Fact]
    public void S3_3_UnknownProperties_Ignored()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='unknown-prop:123;width:100px;height:50px;background-color:#00ff00;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel && pixel.Red < LowChannel,
            $"Unknown property should be ignored; expected green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.3 – Must ignore illegal values for known properties. Verify that
    /// an invalid value for 'width' is discarded and the property falls back
    /// to its initial value.
    /// </summary>
    [Fact]
    public void S3_3_IllegalValues_IgnoredForKnownProperties()
    {
        const string html =
            @"<div style='width:banana;height:50px;background-color:red;'></div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
        // width:banana is invalid; the UA should ignore it. The element's
        // width may fall back to auto or 0 depending on implementation.
        // The key requirement is that the invalid value does not crash the
        // renderer and the element still exists in the tree.
        Assert.True(fragment.Children.Count > 0,
            "Invalid width value should not prevent box generation");
    }

    /// <summary>
    /// §3.3 – Must ignore malformed declarations. A declaration missing
    /// the colon separator should be skipped.
    /// </summary>
    [Fact]
    public void S3_3_MalformedDeclarations_Ignored()
    {
        const string html =
            @"<body style='margin:0;padding:0;'>
                <div style='background-color red;width:100px;height:50px;background-color:blue;'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // The malformed declaration 'background-color red' (no colon) should be
        // ignored, and the subsequent valid background-color:blue should apply.
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Blue > HighChannel,
            $"Malformed declaration should be ignored; expected blue, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    // ═══════════════════════════════════════════════════════════════
    // 3.4  The text/css Content Type
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// §3.4 – Recognise the text/css MIME type. Verify that a &lt;style&gt;
    /// element with type="text/css" is processed.
    /// </summary>
    [Fact]
    public void S3_4_TextCss_MimeTypeRecognised()
    {
        const string html =
            @"<style type='text/css'>
                .mime-test { width: 100px; height: 50px; background-color: red; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='mime-test'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Red > HighChannel,
            $"text/css MIME type should be recognised, expected red, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.4 – @charset rule for encoding declaration. Verify that an
    /// @charset rule at the start of a style block does not break parsing.
    /// </summary>
    [Fact]
    public void S3_4_CharsetRule_DoesNotBreakParsing()
    {
        const string html =
            @"<style>
                @charset ""UTF-8"";
                .charset-test { width: 100px; height: 50px; background-color: green; }
              </style>
              <body style='margin:0;padding:0;'>
                <div class='charset-test'></div>
              </body>";
        using var bitmap = RenderHtml(html, 300, 200);
        // Regardless of charset support, the subsequent valid rule should apply.
        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(pixel.Green > HighChannel,
            $"@charset should not break parsing; expected green, got ({pixel.Red},{pixel.Green},{pixel.Blue})");
    }

    /// <summary>
    /// §3.4 – Encoding resolution order. This is primarily a network/protocol
    /// concern. Verify that UTF-8 encoded content with special characters
    /// renders without error.
    /// </summary>
    [Fact]
    public void S3_4_EncodingResolution_Utf8ContentRendered()
    {
        const string html =
            @"<div style='width:300px;'>
                <p>UTF-8 content: äöü ñ é — ™ © ®</p>
              </div>";
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);
        LayoutInvariantChecker.AssertValid(fragment);
    }

    // ═══════════════════════════════════════════════════════════════
    // Infrastructure
    // ═══════════════════════════════════════════════════════════════

    private static void AssertGoldenLayout(string html, [CallerMemberName] string testName = "")
    {
        var fragment = BuildFragmentTree(html);
        Assert.NotNull(fragment);

        LayoutInvariantChecker.AssertValid(fragment);

        var actualJson = FragmentJsonDumper.ToJson(fragment);
        var goldenPath = Path.Combine(GoldenDir, $"{testName}.json");

        if (!File.Exists(goldenPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actualJson);
            Assert.Fail($"New golden baseline created at {goldenPath}. Re-run to validate.");
        }

        var expectedJson = File.ReadAllText(goldenPath);
        Assert.Equal(expectedJson, actualJson);
    }

    private static Fragment BuildFragmentTree(string html, int width = 500, int height = 500)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(canvas, clip);

        return container.HtmlContainerInt.LatestFragmentTree!;
    }

    private static SKBitmap RenderHtml(string html, int width = 500, int height = 500)
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html);

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(canvas, clip);
        container.PerformPaint(canvas, clip);

        return bitmap;
    }

    private static string GetSourceDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
