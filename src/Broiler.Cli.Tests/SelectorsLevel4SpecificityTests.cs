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
    public void CssParser_DoesNotSplit_Commas_Inside_Is()
    {
        // Was a characterization test over the obsolete DomBridge.CssRules tuple
        // view; rerouted to the shared Broiler.CSS parser (the single source of
        // truth) when that seam was removed at htmlbridge-public-surface/v2. The
        // comma inside :is(...) must not split the rule into two selectors.
        var sheet = new Broiler.CSS.CssParser().ParseStyleSheet(":is(.alpha, .beta) { color: red; }");

        var styleRule = Assert.IsType<Broiler.CSS.CssStyleRule>(Assert.Single(sheet.Rules));
        var selector = Assert.Single(styleRule.Selectors.Selectors);
        Assert.Equal(":is(.alpha, .beta)", selector.Text.Trim());
    }
}
