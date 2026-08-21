using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// What a nested browsing context's own scripts declare, reachable on that frame's <c>window</c>.
/// <para>
/// A sub-document's scripts are evaluated in the shared JS context, so a top-level
/// <c>function foo() {}</c> became a plain global. The frame's <c>window</c> is a different object
/// again, so <c>frames[0].window.foo</c> — how a parent page reaches into a frame it controls —
/// stayed <c>undefined</c> even though the script had run. WPT
/// <c>css-view-transitions/transition-in-empty-iframe</c> drives its whole test that way
/// (<c>frames[0].window.startTransition()</c>), so nothing in the frame ever happened (issue #1552).
/// </para>
/// <para>
/// Publishing the name is only half of it: a shared-context global called from the parent would run
/// in the <em>parent's</em> realm, where <c>document</c> is the parent document. A promoted function
/// therefore re-enters its own frame's context when invoked.
/// </para>
/// </summary>
public class SubDocumentGlobalPublishingTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("broiler-frame-globals").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void WriteChild(string content) =>
        File.WriteAllText(Path.Combine(_directory, "child.html"), content);

    /// <summary>Loads a page with one <c>src</c> frame, runs <paramref name="script"/> after load,
    /// and returns what it assigned to <c>globalThis.result</c>.</summary>
    private string Probe(string script)
    {
        var pageUrl = new Uri(Path.Combine(_directory, "page.html")).AbsoluteUri;
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, """<!DOCTYPE html><body><iframe src="child.html"></iframe></body>""", pageUrl);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();
        context.Eval($"var result = ''; {script}");
        return context.Eval("String(result)").ToString();
    }

    /// <summary>The gap itself: a function the frame declared is a member of the frame's window.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void FrameDeclaredFunction_IsReachableThroughFramesWindow()
    {
        WriteChild("<!DOCTYPE html><body><script>function reveal() {}</script></body>");

        Assert.Equal("function", Probe("result = typeof frames[0].window.reveal;"));
    }

    /// <summary>The same function through the other route onto the frame's global — a parent that
    /// holds the frame element rather than an index.</summary>
    [Fact(Timeout = 600000)]
    public void FrameDeclaredFunction_IsReachableThroughContentWindow()
    {
        WriteChild("<!DOCTYPE html><body><script>function reveal() {}</script></body>");

        Assert.Equal("function",
            Probe("result = typeof document.querySelector('iframe').contentWindow.reveal;"));
    }

    /// <summary>A declared <c>var</c> is published as its value, not wrapped.</summary>
    [Fact(Timeout = 600000)]
    public void FrameDeclaredVariable_IsReachableThroughFramesWindow()
    {
        WriteChild("<!DOCTYPE html><body><script>var answer = 42;</script></body>");

        Assert.Equal("42", Probe("result = frames[0].window.answer;"));
    }

    /// <summary>
    /// Calling into the frame runs in the frame's realm: the function's <c>document</c> is the
    /// frame's document, so "my document" means the frame's, not the caller's.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void CallingAFramesFunction_RunsAgainstTheFramesDocument()
    {
        WriteChild("""
<!DOCTYPE html>
<body>
<div id="marker"></div>
<script>function stamp() { document.getElementById('marker').setAttribute('data-hit', '1'); }</script>
</body>
""");

        Assert.Equal("1", Probe("""
            frames[0].window.stamp();
            result = document.querySelector('iframe').contentDocument
                       .getElementById('marker').getAttribute('data-hit');
        """));
    }

    /// <summary>And the caller's own document is untouched by that call — the realm swap is real,
    /// not a coincidence of both documents carrying the same ids.</summary>
    [Fact(Timeout = 600000)]
    public void CallingAFramesFunction_LeavesTheCallersDocumentAlone()
    {
        WriteChild("""
<!DOCTYPE html>
<body>
<div id="marker"></div>
<script>function stamp() { document.getElementById('marker').setAttribute('data-hit', '1'); }</script>
</body>
""");

        var pageUrl = new Uri(Path.Combine(_directory, "page.html")).AbsoluteUri;
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context,
            """<!DOCTYPE html><body><div id="marker"></div><iframe src="child.html"></iframe></body>""",
            pageUrl);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();
        context.Eval("frames[0].window.stamp();");

        Assert.Equal("null",
            context.Eval("String(document.getElementById('marker').getAttribute('data-hit'))").ToString());
    }

    /// <summary>The bridge's own window members win: a frame declaring <c>document</c> does not
    /// overwrite the frame window's document.</summary>
    [Fact(Timeout = 600000)]
    public void FrameDeclaration_DoesNotOverwriteAWindowMember()
    {
        WriteChild("<!DOCTYPE html><body><script>var document = 7;</script></body>");

        Assert.NotEqual("7", Probe("result = String(frames[0].window.document);"));
    }
}
