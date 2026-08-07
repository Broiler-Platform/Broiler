using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>startViewTransition()</c> on a nested browsing context's document — a page driving a view
/// transition inside its <c>&lt;iframe&gt;</c> through <c>frame.contentDocument</c>.
/// <para>
/// The method was absent from the sub-document surface, so the call threw a <c>TypeError</c> that
/// aborted the rest of the driving script — taking the <em>main</em> frame's own transition down with
/// it. That is why the WPT <c>iframe-and-main-frame-transition-*</c> family's "old main" members
/// scored 0%: the main document never reached its own <c>startViewTransition</c> (issue #1552
/// problems 5 and 6).
/// </para>
/// <para>What those reftests pin is the paused start of the transition:
/// <c>::view-transition-old(root) { opacity: 1 }</c> against
/// <c>::view-transition-new(root) { opacity: 0 }</c>, i.e. the frame shows the document as it stood
/// before the update callback.</para>
/// </summary>
public class SubDocumentViewTransitionTests
{
    /// <summary>The pinning every member of the WPT family uses to freeze the transition showing the
    /// old root snapshot.</summary>
    private const string PinOldVisible = """
<style>
  ::view-transition-group(root) { animation-duration: 300s; }
  ::view-transition-old(root) { animation: unset; opacity: 1; }
  ::view-transition-new(root) { animation: unset; opacity: 0; }
</style>
""";

    /// <summary>The mirror image — the new snapshot is what shows.</summary>
    private const string PinNewVisible = """
<style>
  ::view-transition-group(root) { animation-duration: 300s; }
  ::view-transition-old(root) { animation: unset; opacity: 0; }
  ::view-transition-new(root) { animation: unset; opacity: 1; }
</style>
""";

    /// <summary>
    /// Renders a host page carrying one srcdoc frame, after running <paramref name="script"/>.
    /// Returns the pixel at the centre of the frame and one well outside it, so a case can assert
    /// both the nested document and the main one.
    /// </summary>
    private static ((int R, int G, int B) Frame, (int R, int G, int B) Main) Render(
        string framePinning, string script)
    {
        var html = $$"""
<!DOCTYPE html>
<html class="reftest-wait">
<style>iframe{position:fixed;top:0;left:0;width:200px;height:150px;border:0}</style>
<iframe srcdoc="{{framePinning.Replace("\"", "&quot;").Replace("\n", " ")}}<body></body>"></iframe>
</html>
""";
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///sub-document-view-transition.html");
        context.Eval(script);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        using var bitmap = HtmlRender.RenderToImageWithStyleSet(bridge.SerializeToHtml(), 400, 300);
        var frame = bitmap.GetPixel(100, 75);
        var main = bitmap.GetPixel(300, 250);
        return ((frame.R, frame.G, frame.B), (main.R, main.G, main.B));
    }

    private static readonly (int R, int G, int B) Blue = (0, 0, 255);
    private static readonly (int R, int G, int B) LightBlue = (173, 216, 230);
    private static readonly (int R, int G, int B) LightGreen = (144, 238, 144);

    /// <summary>The gap itself: the method exists on a nested browsing context's document.</summary>
    [Fact]
    public void SubDocument_ExposesStartViewTransition()
    {
        var (_, _) = Render(string.Empty, """
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              document.documentElement.setAttribute('data-kind', typeof cd.startViewTransition);
            }
            onload = runTest;
        """);

        // Asserted through a second render so the attribute survives serialization.
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context,
            """<!DOCTYPE html><html><iframe srcdoc="<body></body>"></iframe></html>""",
            "file:///probe.html");
        context.Eval("""
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              document.documentElement.setAttribute('data-kind', typeof cd.startViewTransition);
            }
            onload = runTest;
        """);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        Assert.Contains("data-kind=\"function\"", bridge.SerializeToHtml());
    }

    /// <summary>
    /// With the old snapshot pinned, the frame shows its pre-callback state — the callback still ran
    /// (it is what makes the transition's "new" state), it is just not what the still captures.
    /// </summary>
    [Fact]
    public void OldSnapshotPinned_FrameShowsThePreCallbackState()
    {
        var (frame, _) = Render(PinOldVisible, """
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              cd.documentElement.style.background = 'blue';
              cd.startViewTransition(function() { cd.documentElement.style.background = 'lightblue'; });
            }
            onload = runTest;
        """);

        Assert.Equal(Blue, frame);
    }

    /// <summary>The mirror: with the new snapshot pinned, the frame shows the post-callback state.
    /// </summary>
    [Fact]
    public void NewSnapshotPinned_FrameShowsThePostCallbackState()
    {
        var (frame, _) = Render(PinNewVisible, """
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              cd.documentElement.style.background = 'blue';
              cd.startViewTransition(function() { cd.documentElement.style.background = 'lightblue'; });
            }
            onload = runTest;
        """);

        Assert.Equal(LightBlue, frame);
    }

    /// <summary>An unpinned transition has run its animation to completion by the time anything is
    /// captured, so the frame shows the new state.</summary>
    [Fact]
    public void UnpinnedTransition_FrameShowsThePostCallbackState()
    {
        var (frame, _) = Render(string.Empty, """
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              cd.documentElement.style.background = 'blue';
              cd.startViewTransition(function() { cd.documentElement.style.background = 'lightblue'; });
            }
            onload = runTest;
        """);

        Assert.Equal(LightBlue, frame);
    }

    /// <summary>
    /// The property the whole family depended on: driving a transition in the frame no longer throws,
    /// so the statements after it — here the main document's own transition — still run.
    /// <para>The two documents pin independently: only the frame carries the old-snapshot pinning, so
    /// the frame holds its pre-callback blue while the main document, unpinned, shows the
    /// post-callback lightgreen. Reaching lightgreen at all is what proves the script ran on; before
    /// this, the main document never got its <c>background</c> set, let alone its transition.</para>
    /// </summary>
    [Fact]
    public void SubDocumentTransition_DoesNotAbortTheDrivingScript()
    {
        var (frame, main) = Render(PinOldVisible, """
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              cd.documentElement.style.background = 'blue';
              cd.startViewTransition(function() { cd.documentElement.style.background = 'lightblue'; });

              document.documentElement.style.background = 'green';
              document.startViewTransition(function() { document.documentElement.style.background = 'lightgreen'; });
            }
            onload = runTest;
        """);

        Assert.Equal(Blue, frame);
        Assert.Equal(LightGreen, main);
    }

    /// <summary>
    /// <c>skipTransition()</c> ends the transition, so nothing is held back and the frame shows its
    /// live state even under the old-snapshot pinning.
    /// </summary>
    [Fact]
    public void SkipTransition_ReleasesTheHeldOldState()
    {
        var (frame, _) = Render(PinOldVisible, """
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              cd.documentElement.style.background = 'blue';
              var t = cd.startViewTransition(function() { cd.documentElement.style.background = 'lightblue'; });
              t.skipTransition();
            }
            onload = runTest;
        """);

        Assert.Equal(LightBlue, frame);
    }

    /// <summary>The returned object carries the promises a caller awaits, all already resolved.
    /// </summary>
    [Fact]
    public void TransitionObject_ResolvesItsPromises()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context,
            """<!DOCTYPE html><html><iframe srcdoc="<body></body>"></iframe></html>""",
            "file:///promises.html");
        context.Eval("""
            function runTest() {
              var cd = document.querySelector('iframe').contentDocument;
              var t = cd.startViewTransition(function() {});
              var seen = [];
              t.ready.then(function(){ seen.push('ready'); });
              t.updateCallbackDone.then(function(){ seen.push('updateCallbackDone'); });
              t.finished.then(function(){ seen.push('finished'); });
              document.documentElement.setAttribute('data-seen', seen.join(','));
            }
            onload = runTest;
        """);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        Assert.Contains("data-seen=\"ready,updateCallbackDone,finished\"", bridge.SerializeToHtml());
    }
}
