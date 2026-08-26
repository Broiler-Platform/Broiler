namespace Broiler.Cli.Tests;

/// <summary>
/// The scrollable extent and scroll range of a container whose axis runs backwards, and the
/// block/inline axis mapping that `scrollIntoView` builds on top of them.
/// </summary>
/// <remarks>
/// <para>
/// The overflow measurement only ever looked toward larger physical coordinates — a descendant's
/// `Right`/`Bottom` against the padding box's `Left`/`Top`. That is the whole story for an axis that
/// grows that way and found nothing at all for one that does not: a `vertical-rl` block axis runs
/// right-to-left, its content overflows to the left, so `scrollWidth` came back equal to
/// `clientWidth`. With no extent there was no range, and every block-axis `scrollIntoView` in a
/// vertical writing mode clamped to zero — the axis mapping above it had been right all along.
/// </para>
/// <para>
/// Every expectation is Chromium's measured answer to the same markup, from one probe run against
/// both. That mattered here more than usual: the construction in
/// <c>GoogleSearchPolyfillTests.ScrollIntoView_Maps_Block_And_Inline_Axes_For_WritingModes</c> puts
/// an absolutely positioned target in a scroller whose content overflows the *other* way, and the
/// two engines legitimately disagree about it — so it could not be used to tell a real mapping bug
/// from a layout difference. These cases keep the target inside the oversized content, where both
/// engines agree and the answer is interpretable.
/// </para>
/// </remarks>
public class ScrollWritingModeGeometryTests
{
    private static string Run(string script)
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title></head>
<body style=""margin:0"">
<div id=""result""></div>
<script>
function scroller(wm, dir) {{
    var s = document.createElement('div');
    s.style.cssText = 'overflow:scroll;width:300px;height:300px';
    if (wm) s.style.writingMode = wm;
    if (dir) s.style.direction = dir;
    var c = document.createElement('div');
    c.style.cssText = 'width:600px;height:600px;position:relative';
    var t = document.createElement('div');
    t.id = 'target';
    t.style.cssText = 'position:absolute;left:200px;top:200px;width:100px;height:100px';
    c.appendChild(t);
    s.appendChild(c);
    document.body.appendChild(s);
    return s;
}}
{script}
</script>
</body>
</html>";
        var rendered = CaptureService.ExecuteScriptsWithDom(html, "https://example.com");
        const string open = "<div id=\"result\">";
        var start = rendered.IndexOf(open, System.StringComparison.Ordinal);
        var end = start < 0 ? -1 : rendered.IndexOf("</div>", start, System.StringComparison.Ordinal);
        return end > 0 ? rendered[(start + open.Length)..end] : "<no #result>";
    }

    /// <summary>
    /// The scrollable extent exists on both axes whichever way they run. `vertical-rl` and
    /// `sideways-rl` reported `scrollWidth == clientWidth` — no extent, so no range at all.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("horizontal-tb", "ltr", "600,600")]
    [InlineData("horizontal-tb", "rtl", "600,600")]
    [InlineData("vertical-rl", "ltr", "600,600")]
    [InlineData("vertical-lr", "ltr", "600,600")]
    [InlineData("sideways-rl", "rtl", "600,600")]
    public void TheScrollableExtentExistsWhicheverWayAnAxisRuns(string wm, string dir, string expected)
        => Assert.Equal(expected, Run(
            $"var s = scroller('{wm}', '{dir}');"
            + "document.getElementById('result').textContent = s.scrollWidth + ',' + s.scrollHeight;"));

    /// <summary>
    /// The scroll range's sign follows the axis direction: a backwards axis runs from
    /// <c>-extent</c> to <c>0</c>, and `sideways-rl` with `direction: rtl` has both axes backwards.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("horizontal-tb", "ltr", "0,0|300,300")]
    [InlineData("horizontal-tb", "rtl", "-300,0|0,300")]
    [InlineData("vertical-rl", "ltr", "-300,0|0,300")]
    [InlineData("vertical-lr", "ltr", "0,0|300,300")]
    [InlineData("sideways-rl", "rtl", "-300,-300|0,0")]
    public void TheScrollRangeSignFollowsTheAxisDirection(string wm, string dir, string expected)
        => Assert.Equal(expected, Run(
            $"var s = scroller('{wm}', '{dir}');"
            + "s.scrollLeft = -99999; s.scrollTop = -99999;"
            + "var min = s.scrollLeft + ',' + s.scrollTop;"
            + "s.scrollLeft = 99999; s.scrollTop = 99999;"
            + "var max = s.scrollLeft + ',' + s.scrollTop;"
            + "document.getElementById('result').textContent = min + '|' + max;"));

    /// <summary>
    /// `block` drives the axis the writing mode makes the block axis, and `inline` the other — with
    /// the physical direction of each following the writing mode and `direction`. Every value is
    /// Chromium's.
    /// </summary>
    [Theory(Timeout = 600000)]
    // Horizontal writing mode: block is vertical, inline is horizontal.
    [InlineData("horizontal-tb", "ltr", "start", "start", "200,200")]
    [InlineData("horizontal-tb", "ltr", "center", "start", "200,100")]
    [InlineData("horizontal-tb", "ltr", "end", "start", "200,0")]
    [InlineData("horizontal-tb", "ltr", "start", "center", "100,200")]
    [InlineData("horizontal-tb", "ltr", "start", "end", "0,200")]
    // ...and `direction: rtl` reverses only the inline (horizontal) one.
    [InlineData("horizontal-tb", "rtl", "start", "start", "-300,200")]
    [InlineData("horizontal-tb", "rtl", "end", "start", "-300,0")]
    [InlineData("horizontal-tb", "rtl", "start", "center", "-200,200")]
    [InlineData("horizontal-tb", "rtl", "start", "end", "-100,200")]
    // Vertical writing mode swaps them: block is horizontal, inline is vertical.
    [InlineData("vertical-rl", "ltr", "start", "start", "-300,200")]
    [InlineData("vertical-rl", "ltr", "center", "start", "-200,200")]
    [InlineData("vertical-rl", "ltr", "end", "start", "-100,200")]
    [InlineData("vertical-rl", "ltr", "start", "center", "-300,100")]
    [InlineData("vertical-rl", "ltr", "start", "end", "-300,0")]
    // `vertical-lr` is the same swap with the block axis running forwards.
    [InlineData("vertical-lr", "ltr", "start", "start", "200,200")]
    [InlineData("vertical-lr", "ltr", "center", "start", "100,200")]
    [InlineData("vertical-lr", "ltr", "end", "start", "0,200")]
    [InlineData("vertical-lr", "ltr", "start", "center", "200,100")]
    [InlineData("vertical-lr", "ltr", "start", "end", "200,0")]
    public void ScrollIntoViewMapsBlockAndInlineOntoThePhysicalAxes(
        string wm, string dir, string block, string inline, string expected)
        => Assert.Equal(expected, Run(
            $"var s = scroller('{wm}', '{dir}');"
            + $"document.getElementById('target').scrollIntoView({{ block: '{block}', inline: '{inline}' }});"
            + "document.getElementById('result').textContent = s.scrollLeft + ',' + s.scrollTop;"));
}
