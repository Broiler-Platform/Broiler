namespace Broiler.Cli.Tests;

/// <summary>
/// CSS Inline Layout §3 "invisible line boxes" (issue #1458, WPT
/// <c>css/css-inline/empty-span-scroll</c>): an inline box whose only content is out of flow — an
/// empty <c>&lt;span&gt;</c> holding just an absolutely-positioned child — still occupies a position
/// on its line. Its geometry must reflect that line position (not the document origin), an
/// absolutely-positioned descendant using it as containing block must anchor to the line, and the
/// (invisible) line box must not add strut height that shifts following blocks.
/// </summary>
public class EmptyInlineGeometryTests
{
    private const string Html = @"<!DOCTYPE html>
<html><head><style>
body { margin: 0; }
.relative { position: relative; }
.scroll-target { position: absolute; top: 0; }
.filler { height: 3000px; background: red; }
.good { height: 100vh; background: green; }
</style></head><body>
<span><div class=""filler""></div></span>
<span><span class=""relative"" id=""rel""><a class=""scroll-target"" id=""target""></a></span></span>
<span><div class=""good"" id=""good""></div></span>
<span><div class=""filler""></div></span>
<div id=""result""></div>
<script>
function top(id){ return Math.round(document.getElementById(id).getBoundingClientRect().top); }
var t = document.getElementById('target');
var targetBefore = top('target');
t.scrollIntoView();
document.getElementById('result').textContent =
  'rel=' + top('rel') + ',target=' + targetBefore + ',good=' + top('good') + ',scrollY=' + Math.round(window.scrollY);
</script>
</body></html>";

    [Fact(Timeout = 600000)]
    public void EmptyRelativeSpan_AnchorsAbsposDescendant_ToItsLine_WithoutAddingStrut()
    {
        var result = CaptureService.ExecuteScriptsWithDom(Html, "file:///test.html");

        // The empty relative <span> sits on the line after the 3000px filler, so it and its
        // absolutely-positioned target both report top ~3000 (not the 0 origin). The green .good
        // block stays at 3000 — the invisible line box adds no strut — and scrollIntoView on the
        // target therefore scrolls the green block to the top of the viewport.
        Assert.Contains("rel=3000,target=3000,good=3000,scrollY=3000", result);
    }
}
