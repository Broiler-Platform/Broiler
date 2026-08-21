using Broiler.HTML.Image;
using Broiler.Layout.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A nested browsing context whose <c>src</c> is an <em>absolute</em> URL on a host the render is
/// told is served from its document root loads that file, instead of painting the empty box a
/// local render has no way to fetch.
/// <para>
/// WPT is the case this exists for. Its cross-origin reftests point a frame at a second host —
/// <c>http://not-web-platform.test:8000/css/…</c> — and WPT serves every one of its hosts from the
/// same checkout, so the URL names a file that is already on disk. Without this the frame painted
/// nothing and the test scored 0.0% against a reference whose frame was empty for the same reason
/// (<c>css/css-color-adjust/…/color-scheme-iframe-background-mismatch-opaque-cross-origin-002.sub</c>).
/// </para>
/// </summary>
public class ServedOriginFrameSrcTests : IDisposable
{
    private const int FrameWidth = 200;
    private const int FrameHeight = 150;

    private static readonly (int R, int G, int B) White = (255, 255, 255);
    private static readonly (int R, int G, int B) Red = (255, 0, 0);
    private static readonly (int R, int G, int B) Blue = (0, 0, 255);

    private const string ServedHost = "web-platform.test";
    private const string AlternateHost = "not-web-platform.test";
    private static readonly string[] ServedHosts = [ServedHost, AlternateHost];

    private readonly string _root =
        Directory.CreateTempSubdirectory("broiler-served-origin").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string RedDocument = "<!DOCTYPE html><body style='margin:0;background:red'></body>";
    private const string BlueDocument = "<!DOCTYPE html><body style='margin:0;background:blue'></body>";

    private void WriteResource(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Page(string frameSrc) => $$"""
<!DOCTYPE html>
<body style="margin:0;background:white">
<style>iframe{width:{{FrameWidth}}px;height:{{FrameHeight}}px;border:0}</style>
<iframe src="{{frameSrc}}"></iframe>
</body>
""";

    /// <summary>
    /// Renders a page holding one frame from a sub-directory of the root, so a URL resolved against
    /// the root and one resolved against the page's own directory cannot accidentally agree.
    /// Returns the pixel at the centre of the frame; white means the frame painted nothing.
    /// </summary>
    private (int R, int G, int B) RenderFrameCentre(string frameSrc, string? documentRoot, string[]? servedHosts)
    {
        var pagePath = Path.Combine(_root, "pages", "page.html");
        Directory.CreateDirectory(Path.GetDirectoryName(pagePath)!);
        File.WriteAllText(pagePath, Page(frameSrc));

        using var pinned = DocumentRoot.Pin(documentRoot, servedHosts);
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            Page(frameSrc), 400, 300, baseUrl: new Uri(pagePath).AbsoluteUri);

        var pixel = bitmap.GetPixel(FrameWidth / 2, FrameHeight / 2);
        return (pixel.R, pixel.G, pixel.B);
    }

    /// <summary>The fix: an absolute URL on a served host loads the file under the root.</summary>
    [Fact(Timeout = 600000)]
    public void Frame_On_A_Served_Host_Loads_From_The_Document_Root()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(Red, RenderFrameCentre(
            $"http://{ServedHost}:8000/css/frame.html", _root, ServedHosts));
    }

    /// <summary>
    /// The whole point of the WPT case: the <em>second</em> origin is served from the same root, so
    /// a cross-origin frame renders the document that host would have returned.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Frame_On_The_Alternate_Host_Loads_From_The_Same_Root()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(Red, RenderFrameCentre(
            $"http://{AlternateHost}:8000/css/frame.html", _root, ServedHosts));
    }

    /// <summary>Any port on a served host is that host: WPT allocates several, and https too.</summary>
    [Theory]
    [InlineData("http", 8000)]
    [InlineData("http", 8001)]
    [InlineData("https", 8443)]
    public void Any_Port_And_Either_Scheme_On_A_Served_Host_Resolves(string scheme, int port)
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(Red, RenderFrameCentre(
            $"{scheme}://{ServedHost}:{port}/css/frame.html", _root, ServedHosts));
    }

    /// <summary>
    /// Null by default: a host that declares no served origins renders exactly as before, so an
    /// absolute URL stays the unfetchable, empty box it has always been.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void No_Served_Hosts_Leaves_An_Absolute_Url_Unresolved()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(White, RenderFrameCentre(
            $"http://{ServedHost}:8000/css/frame.html", _root, servedHosts: null));
    }

    /// <summary>A served host without a root has nothing to resolve against.</summary>
    [Fact(Timeout = 600000)]
    public void No_Document_Root_Leaves_An_Absolute_Url_Unresolved()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(White, RenderFrameCentre(
            $"http://{ServedHost}:8000/css/frame.html", documentRoot: null, ServedHosts));
    }

    /// <summary>
    /// A host that is not on the list is not ours to read off the local disk — including a
    /// subdomain of one that is. WPT mints <c>nonexistent.web-platform.test</c> precisely so that
    /// it fails to resolve; serving it content would turn a load-failure test into a passing one.
    /// </summary>
    [Theory]
    [InlineData("example.com")]
    [InlineData("nonexistent.web-platform.test")]
    [InlineData("web-platform.test.evil.example")]
    public void An_Unlisted_Host_Is_Not_Served(string host)
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(White, RenderFrameCentre(
            $"http://{host}:8000/css/frame.html", _root, ServedHosts));
    }

    /// <summary>A subdomain resolves only when the caller lists it, which is how WPT declares its own.</summary>
    [Fact(Timeout = 600000)]
    public void A_Listed_Subdomain_Is_Served()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(Red, RenderFrameCentre(
            $"http://www.{ServedHost}:8000/css/frame.html", _root,
            [ServedHost, $"www.{ServedHost}"]));
    }

    /// <summary>
    /// Only http(s) is served: a <c>file:</c> or <c>data:</c> URL keeps whatever meaning it already
    /// had rather than being re-pointed at the root.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Non_Http_Scheme_Is_Left_Alone()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(White, RenderFrameCentre(
            $"ftp://{ServedHost}:8000/css/frame.html", _root, ServedHosts));
    }

    /// <summary>
    /// The origin is dropped, not the path's meaning: the URL still names its own file, not one
    /// beside the page. The page lives in <c>pages/</c> and holds a red frame document of its own,
    /// so resolving against the page's directory would paint red instead of blue.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Path_Is_Root_Relative_Not_Page_Relative()
    {
        WriteResource(Path.Combine("pages", "frame.html"), RedDocument);
        WriteResource("frame.html", BlueDocument);

        Assert.Equal(Blue, RenderFrameCentre(
            $"http://{ServedHost}:8000/frame.html", _root, ServedHosts));
    }

    /// <summary>
    /// A query addresses a server that is not there; the path alone names the file. WPT decorates
    /// resource URLs with <c>?pipe=</c> and cache-busters routinely.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Query_Or_Fragment_Does_Not_Stop_The_Resolve()
    {
        WriteResource(Path.Combine("css", "frame.html"), RedDocument);

        Assert.Equal(Red, RenderFrameCentre(
            $"http://{ServedHost}:8000/css/frame.html?pipe=trickle(d1)#top", _root, ServedHosts));
    }

    /// <summary>A served URL that names nothing on disk stays the empty box, rather than throwing.</summary>
    [Fact(Timeout = 600000)]
    public void A_Served_Url_That_Names_Nothing_Paints_Empty()
    {
        Assert.Equal(White, RenderFrameCentre(
            $"http://{ServedHost}:8000/css/missing.html", _root, ServedHosts));
    }

    /// <summary>`..` in a served URL cannot walk out of the document root.</summary>
    [Fact(Timeout = 600000)]
    public void A_Served_Url_Cannot_Escape_The_Root()
    {
        var outside = Path.Combine(Path.GetDirectoryName(_root)!,
            "broiler-served-origin-outside-" + Guid.NewGuid().ToString("N")[..8] + ".html");
        File.WriteAllText(outside, RedDocument);
        try
        {
            Assert.Equal(White, RenderFrameCentre(
                $"http://{ServedHost}:8000/../{Path.GetFileName(outside)}", _root, ServedHosts));
        }
        finally
        {
            File.Delete(outside);
        }
    }
}
