using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

public class SelectorsLevel4SpecificityTests
{
    [Fact]
    public void GetComputedStyle_Is_Uses_MostSpecific_Argument()
    {
        var html = """
<!DOCTYPE html>
<html><head>
<style>
.box { z-index: 1; }
:is(.other, #target) { z-index: 2; }
</style>
</head><body>
<div id="target" class="box"></div>
<div id="result"></div>
<script>
document.getElementById('result').textContent =
  window.getComputedStyle(document.getElementById('target')).zIndex;
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">2<", result);
    }

    [Fact]
    public void GetComputedStyle_Where_Has_Zero_Specificity()
    {
        var html = """
<!DOCTYPE html>
<html><head>
<style>
.card { z-index: 1; }
:where(#target) { z-index: 2; }
</style>
</head><body>
<div id="target" class="card"></div>
<div id="result"></div>
<script>
document.getElementById('result').textContent =
  window.getComputedStyle(document.getElementById('target')).zIndex;
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">1<", result);
    }

    [Fact]
    public void GetComputedStyle_Has_Uses_Argument_Specificity()
    {
        var html = """
<!DOCTYPE html>
<html><head>
<style>
.card { z-index: 1; }
div:has(#child) { z-index: 2; }
</style>
</head><body>
<div id="target" class="card"><span id="child"></span></div>
<div id="result"></div>
<script>
document.getElementById('result').textContent =
  window.getComputedStyle(document.getElementById('target')).zIndex;
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">2<", result);
    }

    [Fact]
    public void GetComputedStyle_NthChild_OfSelector_Adds_Filter_Specificity()
    {
        var html = """
<!DOCTYPE html>
<html><head>
<style>
.match { z-index: 1; }
p:nth-child(1 of #featured) { z-index: 2; }
</style>
</head><body>
<div>
  <p id="featured" class="match">target</p>
  <p>other</p>
</div>
<div id="result"></div>
<script>
document.getElementById('result').textContent =
  window.getComputedStyle(document.getElementById('featured')).zIndex;
</script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains(">2<", result);
    }

    [Fact]
    public void DomBridge_CssRules_DoNotSplit_Commas_Inside_Is()
    {
        const string html = """
<!DOCTYPE html>
<html><head>
<style>
:is(.alpha, .beta) { color: red; }
</style>
</head><body><div class="alpha"></div></body></html>
""";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");

        var rule = Assert.Single(bridge.CssRules);
        Assert.Equal(":is(.alpha, .beta)", rule.Selector);
    }
}
