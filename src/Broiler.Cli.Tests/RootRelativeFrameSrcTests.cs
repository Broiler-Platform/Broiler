using Broiler.HTML.Image;
using Broiler.Layout.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A nested browsing context whose <c>src</c> is root-relative (<c>/a/b.html</c>) resolves against
/// the host's document root, not against the directory the containing page sits in.
/// <para>
/// HTML §"resolve a URL" resolves a leading <c>/</c> against the document's origin. A
/// <c>file://</c> render has no origin to ask, and the fragment builder joined such a URL onto the
/// containing directory like any other relative reference — where <c>Path.Combine</c> discards its
/// left operand for a rooted right one, yielding a path at the filesystem root that exists nowhere.
/// Every such frame therefore painted empty, WPT
/// <c>resource-timing/initiator-type/frameset</c> among them: its whole visible content is one
/// <c>&lt;frame src="/resource-timing/resources/green.html"&gt;</c>.
/// </para>
/// </summary>
public class RootRelativeFrameSrcTests : IDisposable
{
    private const int FrameWidth = 200;
    private const int FrameHeight = 150;

    private static readonly (int R, int G, int B) Red = (255, 0, 0);
    private static readonly (int R, int G, int B) Blue = (0, 0, 255);

    /// <summary>The document root — <c>/…</c> resolves against this.</summary>
    private readonly string _root =
        Directory.CreateTempSubdirectory("broiler-root-relative").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteResource(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Page(string frameSrc) => $$"""
<!DOCTYPE html>
<body style="margin:0;background:white">
<style>iframe{width:{{FrameWidth}}px;height:{{FrameHeight}}px;border:0}</style>
<iframe src="{{frameSrc}}"></iframe>
</body>
""";

    /// <summary>
    /// Renders a page holding one frame, from a sub-directory of the root (so a root-relative URL
    /// and a directory-relative one cannot accidentally agree), and returns the pixel at the centre
    /// of the frame's content box. White means the frame painted nothing.
    /// </summary>
    private (int R, int G, int B) RenderFrameCentre(string frameSrc, string? documentRoot)
    {
        var pagePath = WriteResource(Path.Combine("pages", "page.html"), Page(frameSrc));
        var pageUrl = new Uri(pagePath).AbsoluteUri;

        using var pinned = DocumentRoot.Pin(documentRoot);
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Page(frameSrc), 400, 300, baseUrl: pageUrl);

        var pixel = bitmap.GetPixel(FrameWidth / 2, FrameHeight / 2);
        return (pixel.R, pixel.G, pixel.B);
    }

    private const string RedDocument = "<!DOCTYPE html><body style='margin:0;background:red'></body>";
    private const string BlueDocument = "<!DOCTYPE html><body style='margin:0;background:blue'></body>";

    /// <summary>The fix: with a root set, the frame loads the file that root names.</summary>
    [Fact]
    public void RootRelativeSrc_ResolvesAgainstTheDocumentRoot()
    {
        WriteResource(Path.Combine("resources", "child.html"), RedDocument);
        Assert.Equal(Red, RenderFrameCentre("/resources/child.html", _root));
    }

    /// <summary>
    /// The negative half, and the reason this is a host-supplied root rather than a guess: with no
    /// root set the URL does not resolve and the frame paints nothing — exactly what it did before.
    /// </summary>
    [Fact]
    public void RootRelativeSrc_WithoutADocumentRoot_PaintsNothing()
    {
        WriteResource(Path.Combine("resources", "child.html"), RedDocument);
        Assert.NotEqual(Red, RenderFrameCentre("/resources/child.html", documentRoot: null));
    }

    /// <summary>
    /// The root is not the page's own directory: a root-relative URL must not quietly behave like a
    /// directory-relative one. The page lives in <c>pages/</c>, so this resolves only from the root.
    /// </summary>
    [Fact]
    public void RootRelativeSrc_DoesNotResolveAgainstThePagesOwnDirectory()
    {
        WriteResource(Path.Combine("pages", "sibling.html"), RedDocument);
        Assert.NotEqual(Red, RenderFrameCentre("/sibling.html", _root));
    }

    /// <summary>
    /// A query addresses a server that is not there; the path alone names the file. WPT leans on
    /// this — <c>?pipe=</c> decorates the URL of a resource that is still just a file on disk.
    /// </summary>
    [Theory]
    [InlineData("/resources/child.html?pipe=trickle(d1)")]
    [InlineData("/resources/child.html#fragment")]
    [InlineData("/resources/child.html?a=1#b")]
    public void RootRelativeSrc_IgnoresTheQueryAndFragment(string src)
    {
        WriteResource(Path.Combine("resources", "child.html"), RedDocument);
        Assert.Equal(Red, RenderFrameCentre(src, _root));
    }

    /// <summary>A percent-escaped path names the file it decodes to.</summary>
    [Fact]
    public void RootRelativeSrc_UnescapesThePath()
    {
        WriteResource(Path.Combine("resources", "a b.html"), RedDocument);
        Assert.Equal(Red, RenderFrameCentre("/resources/a%20b.html", _root));
    }

    /// <summary>
    /// A <c>..</c> segment must not walk out of the root: served over HTTP it could not, and the
    /// frame would 404 rather than reach an unrelated file elsewhere on the disk.
    /// </summary>
    [Fact]
    public void RootRelativeSrc_CannotEscapeTheRoot()
    {
        var outside = Path.Combine(Path.GetDirectoryName(_root.TrimEnd(Path.DirectorySeparatorChar))!,
            $"broiler-outside-{Path.GetFileName(_root)}.html");
        File.WriteAllText(outside, RedDocument);
        try
        {
            var escaping = "/../" + Path.GetFileName(outside);
            Assert.NotEqual(Red, RenderFrameCentre(escaping, _root));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    /// <summary>
    /// <c>//host/path</c> is scheme-relative, not root-relative: it names another origin and is not
    /// ours to read off the local disk. Two slashes must not be mistaken for one.
    /// </summary>
    [Fact]
    public void SchemeRelativeSrc_IsNotTreatedAsRootRelative()
    {
        WriteResource(Path.Combine("resources", "child.html"), RedDocument);
        Assert.NotEqual(Red, RenderFrameCentre("//example.test/resources/child.html", _root));
    }

    /// <summary>
    /// A bare <c>/</c> names a directory, not a document, and resolves to nothing rather than to the
    /// root itself.
    /// </summary>
    [Fact]
    public void RootRelativeSrc_OfJustASlash_PaintsNothing()
    {
        WriteResource(Path.Combine("resources", "child.html"), RedDocument);
        Assert.NotEqual(Red, RenderFrameCentre("/", _root));
    }

    /// <summary>
    /// An ordinary relative <c>src</c> keeps resolving against the containing directory whether or
    /// not a root is set — the path that already worked is untouched by this.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RelativeSrc_StillResolvesAgainstTheContainingDirectory(bool withRoot)
    {
        WriteResource(Path.Combine("pages", "sibling.html"), BlueDocument);
        Assert.Equal(Blue, RenderFrameCentre("sibling.html", withRoot ? _root : null));
    }

    /// <summary>
    /// The lever restores what it found rather than leaving its own value behind, so a nested
    /// render (a frame inside a frame) cannot strand a root on the thread.
    /// </summary>
    [Fact]
    public void Pin_RestoresThePreviousRoot()
    {
        Assert.Null(DocumentRoot.Current);

        using (DocumentRoot.Pin("/outer"))
        {
            Assert.Equal("/outer", DocumentRoot.Current);

            using (DocumentRoot.Pin("/inner"))
                Assert.Equal("/inner", DocumentRoot.Current);

            Assert.Equal("/outer", DocumentRoot.Current);
        }

        Assert.Null(DocumentRoot.Current);
    }
}
