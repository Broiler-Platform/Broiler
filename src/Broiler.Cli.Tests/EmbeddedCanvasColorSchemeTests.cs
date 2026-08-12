using Broiler.HTML.Image;
using Broiler.Layout.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Color Adjust 1 §2.4: a nested browsing context's canvas is transparent — the embedder shows
/// through it — unless the <em>embedding element's</em> used colour scheme differs from the embedded
/// root's, in which case the UA paints an opaque backdrop of the embedded root's scheme.
/// <para>
/// Both halves were missing. The frame's canvas was always resolved opaque, so a matching pair
/// painted a box that should not have been there (WPT
/// <c>color-scheme-iframe-background</c>, <c>…-mismatch-opaque-cross-origin-003.sub</c>); and
/// <c>color-scheme</c> did not inherit, so an <c>&lt;iframe&gt;</c> under
/// <c>html { color-scheme: dark }</c> reported <c>normal</c> and the comparison was made against the
/// wrong value.
/// </para>
/// </summary>
public class EmbeddedCanvasColorSchemeTests : IDisposable
{
    private const int FrameWidth = 200;
    private const int FrameHeight = 150;
    private const int PageWidth = 400;
    private const int PageHeight = 300;

    /// <summary>The UA dark canvas backdrop (CSS Color Adjust §2.3).</summary>
    private static readonly (int R, int G, int B) UaDark = (18, 18, 18);
    private static readonly (int R, int G, int B) White = (255, 255, 255);
    private static readonly (int R, int G, int B) Red = (255, 0, 0);
    private static readonly (int R, int G, int B) Blue = (0, 0, 255);

    private readonly string _root =
        Directory.CreateTempSubdirectory("broiler-embedded-canvas").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteFrame(string name, string content) =>
        File.WriteAllText(Path.Combine(_root, name), content);

    /// <summary>A frame document that propagates no background of its own.</summary>
    private static string FrameDocument(string? colorScheme) =>
        colorScheme is null
            ? "<!DOCTYPE html><p>frame</p>"
            : $"<!DOCTYPE html><style>:root{{color-scheme:{colorScheme}}}</style><p>frame</p>";

    /// <summary>
    /// Renders a page whose body has <paramref name="pageBackground"/> and holds one frame, and
    /// returns the pixel at the centre of the frame's content box.
    /// </summary>
    private (int R, int G, int B) RenderFrameCentre(
        string pageBackground, string? rootColorScheme, string? iframeColorScheme, string frameSrc)
    {
        var rootStyle = rootColorScheme is null ? "" : $"html{{color-scheme:{rootColorScheme}}}";
        var frameStyle = iframeColorScheme is null ? "" : $"color-scheme:{iframeColorScheme};";
        var page = $$"""
<!DOCTYPE html>
<style>
  {{rootStyle}}
  body { margin: 0; background: {{pageBackground}} }
  iframe { width: {{FrameWidth}}px; height: {{FrameHeight}}px; border: 0; {{frameStyle}} }
</style>
<iframe src="{{frameSrc}}"></iframe>
""";
        var pagePath = Path.Combine(_root, "page.html");
        File.WriteAllText(pagePath, page);

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            page, PageWidth, PageHeight, baseUrl: new Uri(pagePath).AbsoluteUri);

        var pixel = bitmap.GetPixel(FrameWidth / 2, FrameHeight / 2);
        return (pixel.R, pixel.G, pixel.B);
    }

    /// <summary>
    /// Whether the pinned <c>Broiler.HTML</c> can leave a frame's canvas transparent. The renderer
    /// half of this is upstream and pinned (<c>Broiler.HTML</c> <c>d1cdad4</c>), so the probe now
    /// succeeds and the render assertions below are live guards. It stays a probe rather than an
    /// assumption because that is what makes the suite honest against a *future* pointer that
    /// predates the fix — the same shape as <c>TemplateContentInertnessTests</c>. Before it landed,
    /// every frame was composited opaque and those assertions could not hold.
    /// </summary>
    private bool FrameCanvasCanBeTransparent()
    {
        WriteFrame("probe-frame.html", FrameDocument("light"));
        return RenderFrameCentre("red", rootColorScheme: null,
            iframeColorScheme: "light", "probe-frame.html") == Red;
    }

    // ── The canvas is transparent when the schemes agree ─────────────────────

    /// <summary>
    /// The bug this fixes, in its simplest form: a light frame in a light-scheme
    /// <c>&lt;iframe&gt;</c> on a red page shows the red page, not a white box.
    /// </summary>
    [Fact]
    public void A_Matching_Light_Frame_Is_Transparent()
    {
        if (!FrameCanvasCanBeTransparent())
            return; // patch 0004 not applied; see FrameCanvasCanBeTransparent.

        WriteFrame("frame.html", FrameDocument("light"));

        Assert.Equal(Red, RenderFrameCentre("red", rootColorScheme: null,
            iframeColorScheme: "light", "frame.html"));
    }

    /// <summary>
    /// WPT <c>color-scheme-iframe-background</c>: a dark frame in a dark-scheme
    /// <c>&lt;iframe&gt;</c> on a light page stays transparent, so the light page shows through
    /// rather than the UA dark backdrop.
    /// </summary>
    [Fact]
    public void A_Matching_Dark_Frame_Is_Transparent()
    {
        if (!FrameCanvasCanBeTransparent())
            return; // patch 0004 not applied; see FrameCanvasCanBeTransparent.

        WriteFrame("frame.html", FrameDocument("dark"));

        Assert.Equal(White, RenderFrameCentre("white", rootColorScheme: null,
            iframeColorScheme: "dark", "frame.html"));
    }

    /// <summary>
    /// WPT <c>…-mismatch-opaque-cross-origin-003.sub</c>: the embedding element's own
    /// <c>color-scheme</c> is what counts, not the document's. A light <c>&lt;iframe&gt;</c> under a
    /// dark root, holding a light frame, agrees with its frame — so the dark page shows through.
    /// </summary>
    [Fact]
    public void The_Comparison_Is_Against_The_Embedding_Element_Not_The_Document()
    {
        if (!FrameCanvasCanBeTransparent())
            return; // patch 0004 not applied; see FrameCanvasCanBeTransparent.

        WriteFrame("frame.html", FrameDocument("light"));

        Assert.Equal(UaDark, RenderFrameCentre("transparent", rootColorScheme: "dark",
            iframeColorScheme: "light", "frame.html"));
    }

    // ── …and opaque when they differ ─────────────────────────────────────────

    /// <summary>
    /// WPT <c>…-mismatch-opaque</c>: a dark frame in a light <c>&lt;iframe&gt;</c> gets the UA dark
    /// backdrop, so the red page does *not* show through.
    /// </summary>
    [Fact]
    public void A_Dark_Frame_In_A_Light_Element_Gets_The_Dark_Backdrop()
    {
        WriteFrame("frame.html", FrameDocument("dark"));

        Assert.Equal(UaDark, RenderFrameCentre("red", rootColorScheme: null,
            iframeColorScheme: null, "frame.html"));
    }

    /// <summary>
    /// WPT <c>…-mismatch-opaque-cross-origin-002.sub</c>: a light frame in a dark <c>&lt;iframe&gt;</c>
    /// gets an opaque white canvas, hiding the dark page behind it. This is the case that
    /// <c>color-scheme</c> inheritance decides — the iframe declares nothing and takes <c>dark</c>
    /// from the root.
    /// </summary>
    [Fact]
    public void A_Light_Frame_Under_An_Inherited_Dark_Scheme_Gets_An_Opaque_White_Canvas()
    {
        WriteFrame("frame.html", FrameDocument("light"));

        Assert.Equal(White, RenderFrameCentre("transparent", rootColorScheme: "dark",
            iframeColorScheme: null, "frame.html"));
    }

    /// <summary>
    /// A frame that propagates a background of its own paints it whether the canvas is transparent
    /// or not — only the UA's backdrop is dropped, never the document's own painting.
    /// </summary>
    [Fact]
    public void A_Frames_Own_Background_Still_Paints_When_The_Canvas_Is_Transparent()
    {
        if (!FrameCanvasCanBeTransparent())
            return; // patch 0004 not applied; see FrameCanvasCanBeTransparent.

        WriteFrame("frame.html",
            "<!DOCTYPE html><style>:root{color-scheme:light;background:blue}</style><p>frame</p>");

        Assert.Equal(Blue, RenderFrameCentre("red", rootColorScheme: null,
            iframeColorScheme: "light", "frame.html"));
    }

    /// <summary>
    /// A frame that never loaded has no canvas at all, so the embedder shows through — the empty
    /// box behaviour an unresolvable <c>src</c> has always had.
    /// </summary>
    [Fact]
    public void An_Unloadable_Frame_Leaves_The_Page_Showing()
    {
        Assert.Equal(Red, RenderFrameCentre("red", rootColorScheme: null,
            iframeColorScheme: null, "missing.html"));
    }

    // ── color-scheme inherits (CSS Color Adjust §2.1) ────────────────────────

    /// <summary>
    /// Walks the laid-out fragment tree for the first fragment of <paramref name="tagName"/> and
    /// returns its computed <c>color-scheme</c>.
    /// </summary>
    private static string? ComputedColorScheme(string html, string tagName)
    {
        using var container = new HtmlContainer
        {
            Location = new System.Drawing.PointF(0, 0),
            MaxSize = new System.Drawing.SizeF(PageWidth, PageHeight),
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
        container.SetHtmlWithStyleSet(html);
        container.PerformLayout(new System.Drawing.RectangleF(0, 0, PageWidth, PageHeight));

        static Broiler.Layout.IR.Fragment? Find(Broiler.Layout.IR.Fragment? fragment, string tag)
        {
            if (fragment is null)
                return null;
            if (string.Equals(fragment.Style.TagName, tag, StringComparison.OrdinalIgnoreCase))
                return fragment;

            foreach (var child in fragment.Children)
                if (Find(child, tag) is { } found)
                    return found;

            return null;
        }

        return Find(container.LatestFragmentTree, tagName)?.Style.ColorScheme;
    }

    /// <summary>
    /// §2.1 makes <c>color-scheme</c> an inherited property. It was read only off the root element,
    /// which inherits nothing, so its absence from the inherited set went unnoticed until an
    /// <c>&lt;iframe&gt;</c> had to be asked for its own used scheme.
    /// </summary>
    [Fact]
    public void ColorScheme_Inherits_To_Descendants()
    {
        const string html = """
<!DOCTYPE html>
<style>html { color-scheme: dark }</style>
<div><span id="deep">x</span></div>
""";
        Assert.Equal("dark", ComputedColorScheme(html, "div"));
        Assert.Equal("dark", ComputedColorScheme(html, "span"));
    }

    /// <summary>A descendant's own declaration still wins over the inherited value.</summary>
    [Fact]
    public void A_Descendants_Own_ColorScheme_Overrides_The_Inherited_One()
    {
        const string html = """
<!DOCTYPE html>
<style>html { color-scheme: dark } div { color-scheme: light }</style>
<div>x</div>
""";
        Assert.Equal("light", ComputedColorScheme(html, "div"));
    }

    /// <summary>With nothing declared anywhere the computed value stays the initial one.</summary>
    [Fact]
    public void ColorScheme_Defaults_To_Normal()
    {
        Assert.Equal("normal", ComputedColorScheme("<!DOCTYPE html><div>x</div>", "div"));
    }

    // ── The rule itself ──────────────────────────────────────────────────────

    /// <summary>
    /// A document that is not embedded always paints its backdrop, so nothing about a top-level
    /// render changes: this is what keeps the lever inert everywhere it is not pinned.
    /// </summary>
    [Fact]
    public void A_Document_That_Is_Not_Embedded_Always_Paints_Its_Backdrop()
    {
        Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop("dark"));
        Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop("light"));
        Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop(null));
    }

    [Fact]
    public void The_Backdrop_Is_Painted_Exactly_When_The_Used_Schemes_Differ()
    {
        using (EmbeddedCanvas.Pin("dark"))
        {
            Assert.False(EmbeddedCanvas.PaintsOpaqueBackdrop("dark"));
            Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop("light"));
            Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop("normal"));
            Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop(null));
        }

        using (EmbeddedCanvas.Pin("light"))
        {
            Assert.True(EmbeddedCanvas.PaintsOpaqueBackdrop("dark"));
            Assert.False(EmbeddedCanvas.PaintsOpaqueBackdrop("light"));
            // `normal` and an absent value are both "not dark", so they agree with `light`.
            Assert.False(EmbeddedCanvas.PaintsOpaqueBackdrop("normal"));
            Assert.False(EmbeddedCanvas.PaintsOpaqueBackdrop(null));
        }
    }

    /// <summary>
    /// §2.2–2.3: a used scheme is dark when the list offers <c>dark</c> and not <c>light</c>; a
    /// <c>light dark</c> pair resolves to the environment's light preference, and <c>only</c> does
    /// not change the outcome.
    /// </summary>
    [Theory]
    [InlineData("dark", true)]
    [InlineData("only dark", true)]
    [InlineData("dark normal", true)]
    [InlineData("light dark", false)]
    [InlineData("dark light", false)]
    [InlineData("light", false)]
    [InlineData("normal", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UsesDarkColorScheme_Resolves_Against_A_Light_Preference(string? value, bool expected) =>
        Assert.Equal(expected, EmbeddedCanvas.UsesDarkColorScheme(value));

    /// <summary>Nesting restores, so sibling frames do not inherit one another's embedder.</summary>
    [Fact]
    public void Pinning_Nests_And_Restores()
    {
        Assert.False(EmbeddedCanvas.IsEmbedded);

        using (EmbeddedCanvas.Pin("dark"))
        {
            Assert.True(EmbeddedCanvas.IsEmbedded);
            Assert.Equal("dark", EmbeddedCanvas.EmbedderColorScheme);

            using (EmbeddedCanvas.Pin("light"))
                Assert.Equal("light", EmbeddedCanvas.EmbedderColorScheme);

            Assert.Equal("dark", EmbeddedCanvas.EmbedderColorScheme);
        }

        Assert.False(EmbeddedCanvas.IsEmbedded);
        Assert.Null(EmbeddedCanvas.EmbedderColorScheme);
    }
}
