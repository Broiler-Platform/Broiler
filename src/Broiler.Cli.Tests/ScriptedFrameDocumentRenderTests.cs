using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A frame loaded from <c>src</c> renders the document it has become, not the file it came from.
/// <para>
/// A nested browsing context reaches the renderer as markup on its container element. An
/// <c>&lt;iframe srcdoc&gt;</c> round-trips through its attribute, but a <c>src</c> frame had no such
/// carrier: the fragment builder re-read the resource from disk, so every mutation the frame's own
/// scripts — or a parent reaching in through <c>frames[0]</c> — had made to the live sub-document was
/// discarded at paint time.
/// </para>
/// <para>
/// WPT <c>css-view-transitions/transition-in-empty-iframe</c> is that shape: the parent calls
/// <c>frames[0].window.startTransition()</c> and the child's update callback unhides a box, but the
/// frame kept painting the file's hidden box (issue #1552).
/// </para>
/// </summary>
public class ScriptedFrameDocumentRenderTests : IDisposable
{
    private const int FrameWidth = 200;
    private const int FrameHeight = 150;

    private readonly string _directory =
        Directory.CreateTempSubdirectory("broiler-scripted-frame").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string WriteResource(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Renders a host page carrying one <c>src</c> frame after running <paramref name="script"/>,
    /// and returns the pixel at the centre of the frame's content box.
    /// </summary>
    private (int R, int G, int B) RenderFrameCentre(string frameSrc, string script = "")
    {
        var pageUrl = new Uri(Path.Combine(_directory, "page.html")).AbsoluteUri;
        var html = $$"""
<!DOCTYPE html>
<body style="margin:0;background:green">
<style>iframe{width:{{FrameWidth}}px;height:{{FrameHeight}}px;border:0}</style>
<iframe src="{{frameSrc}}"></iframe>
</body>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, pageUrl);
        if (!string.IsNullOrEmpty(script))
            context.Eval(script);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(
            bridge.SerializeToHtml(), 400, 300, baseUrl: pageUrl);
        var pixel = bitmap.GetPixel(FrameWidth / 2, FrameHeight / 2);
        return (pixel.R, pixel.G, pixel.B);
    }

    private static readonly (int R, int G, int B) Blue = (0, 0, 255);
    private static readonly (int R, int G, int B) Red = (255, 0, 0);
    private static readonly (int R, int G, int B) LimeGreen = (50, 205, 50);

    /// <summary>An untouched frame still renders straight from its resource — the path that already
    /// worked keeps working.</summary>
    [Fact(Timeout = 600000)]
    public void UnscriptedSrcFrame_RendersItsResource()
    {
        WriteResource("child.html", "<!DOCTYPE html><body style='margin:0;background:red'></body>");
        Assert.Equal(Red, RenderFrameCentre("child.html"));
    }

    /// <summary>The bug: a parent mutating the frame's document through
    /// <c>contentDocument</c> changes what the frame paints.</summary>
    [Fact(Timeout = 600000)]
    public void ParentMutatingContentDocument_RendersTheMutation()
    {
        WriteResource("child.html", "<!DOCTYPE html><body style='margin:0;background:red'></body>");

        Assert.Equal(Blue, RenderFrameCentre("child.html", """
            onload = function () {
              var body = document.querySelector('iframe').contentDocument.body;
              body.setAttribute('style', 'margin:0;background:blue');
            };
        """));
    }

    /// <summary>The shape <c>transition-in-empty-iframe</c> drives: the parent calls a function the
    /// frame declared, which unhides content inside the frame.</summary>
    [Fact(Timeout = 600000)]
    public void ParentCallingIntoFrame_RendersWhatTheFrameDid()
    {
        WriteResource("child.html", """
<!DOCTYPE html>
<html>
<head>
<style>
  body { margin: 0 }
  div { width: 100vw; height: 100vh; background-color: limegreen }
  .hidden { display: none }
</style>
<script>
  function reveal() {
    document.querySelector('.hidden').classList.remove('hidden');
  }
</script>
</head>
<body style="background:red"><div class="hidden"></div></body>
</html>
""");

        Assert.Equal(LimeGreen, RenderFrameCentre("child.html", """
            onload = function () { frames[0].window.reveal(); };
        """));
    }

    /// <summary>A frame's own load-time script counts too: what it did to its document is what the
    /// frame shows.</summary>
    [Fact(Timeout = 600000)]
    public void FrameMutatingItselfOnLoad_RendersTheMutation()
    {
        WriteResource("child.html", """
<!DOCTYPE html>
<body style="margin:0;background:red">
<script>document.body.setAttribute('style', 'margin:0;background:blue');</script>
</body>
""");

        Assert.Equal(Blue, RenderFrameCentre("child.html"));
    }

    /// <summary>
    /// A mutated frame still resolves its own relative references against the resource it was loaded
    /// from, not against the containing page — the frame travels to the renderer as markup, so it has
    /// to carry that URL with it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void MutatedFrame_ResolvesRelativeUrlsAgainstItsOwnResource()
    {
        // Same file name in both directories, different colours: only the frame's own copy is blue.
        WriteResource("child.css", "body { background: red }");
        WriteResource(Path.Combine("nested", "child.css"), "body { background: blue }");
        WriteResource(Path.Combine("nested", "child.html"), """
<!DOCTYPE html>
<html><head><link rel="stylesheet" href="child.css"></head>
<body style="margin:0"></body></html>
""");

        Assert.Equal(Blue, RenderFrameCentre("nested/child.html", """
            onload = function () {
              document.querySelector('iframe').contentDocument.body.setAttribute('data-marked', '1');
            };
        """));
    }

    /// <summary>
    /// A frame with no resource of its own is left alone: the stamp stands in for the renderer's
    /// loading of a resource, and <c>about:blank</c> has none. Scripting content into such a frame
    /// still paints nothing — a separate gap this change deliberately does not reach into, and the
    /// case that keeps WPT <c>input-targets-root-while-render-blocked</c> from regressing.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScriptedBlankFrame_IsLeftToTheRenderer()
    {
        Assert.Equal((0, 128, 0), RenderFrameCentre("about:blank", """
            onload = function () {
              var doc = document.querySelector('iframe').contentDocument;
              doc.body.setAttribute('style', 'margin:0;background:blue');
            };
        """));
    }
}
