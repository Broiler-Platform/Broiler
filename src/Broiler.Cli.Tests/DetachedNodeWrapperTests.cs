namespace Broiler.Cli.Tests;

/// <summary>
/// A node the page still holds after it has left the tree keeps answering its members, as it does in
/// a browser.
/// </summary>
/// <remarks>
/// <para>
/// Wrapper identity lives in <c>JsObjectRegistry</c>, and a member that lives on an interface
/// prototype has no node captured in a closure — it looks its receiver up there on every call. An
/// <c>innerHTML</c> assignment unregisters the whole subtree it replaces, to release the wrappers it
/// is discarding, and that dropped the reverse entry as well: every <em>inherited</em> member on a
/// node the page had a reference to became <c>TypeError: Illegal invocation</c>, while the members
/// the wrapper still owned went on working. So the failure grew with each interface that moved onto a
/// prototype, and it is silent — the page holds a node that looks fine and throws on use.
/// </para>
/// <para>
/// The reverse map is weakly keyed now and outlives the unregistration: the node stays reachable for
/// exactly as long as script can still reach the wrapper, which is the case a browser keeps working,
/// and is released with it otherwise. The forward map is still dropped, so the registry stops holding
/// the pair outright.
/// </para>
/// </remarks>
public class DetachedNodeWrapperTests
{
    private static string Run(string script)
    {
        var html = $@"<!DOCTYPE html><html><body>
<div id=""d""><span id=""s"" class=""c"">hi</span></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
document.getElementById('result').textContent = String({script});
</script></body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
    }

    /// <summary>
    /// The whole surface, on a node <c>innerHTML</c> replaced. Chromium answers the same for each:
    /// the node is detached, not dead.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnInnerHtmlAssignmentDoesNotBreakTheNodeItReplaced()
        => Assert.Contains(">1|SPAN|s|c|hi|null|false<", Run(
            "(function () { var gone = d.firstElementChild; d.innerHTML = 'fresh';"
            + "return [gone.nodeType, gone.tagName, gone.getAttribute('id'), gone.className,"
            + " gone.textContent, String(gone.parentNode), gone.isConnected].join('|'); })()"));

    /// <summary>And it is still writable, not merely readable.</summary>
    [Fact(Timeout = 600000)]
    public void TheReplacedNodeCanStillBeMutatedAndReattached()
        => Assert.Contains(">1|SPAN|SPAN|true<", Run(
            "(function () { var gone = d.firstElementChild; d.innerHTML = 'fresh';"
            + "gone.setAttribute('z', '1');"
            + "return [gone.getAttribute('z'), gone.cloneNode(true).tagName,"
            + " (function () { d.appendChild(gone); return d.lastElementChild.tagName; })(),"
            + " gone.isConnected].join('|'); })()"));

    /// <summary>
    /// The ordinary removals never had the problem — they leave the registry alone — so this pins
    /// that they still do not.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnOrdinaryRemovalKeepsTheNodeWorkingToo()
        => Assert.Contains(">1|SPAN|s|true<", Run(
            "(function () { var gone = d.firstElementChild; var same = gone; d.removeChild(gone);"
            + "return [gone.nodeType, gone.tagName, gone.getAttribute('id'),"
            + " same === d.querySelector('#s') || d.querySelector('#s') === null].join('|'); })()"));
}
