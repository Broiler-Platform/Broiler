using Broiler.Cli;

namespace Broiler.Cli.Tests;

public class SelectorScopeTests
{
    [Fact]
    public void Element_QuerySelectorAll_Scope_Child_Combinator_Finds_Direct_Children()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
  <div id="host">
    <span id="direct" class="item"></span>
    <div><span id="nested" class="item"></span></div>
  </div>
  <script>
    (() => {
      var host = document.getElementById('host');
      var direct = host.querySelectorAll(':scope > .item');
      document.body.setAttribute('data-result', [direct.length, direct[0].id, host.querySelector(':scope > .item').id].join('|'));
    })();
  </script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("data-result=\"1|direct|direct\"", result);
    }

    [Fact]
    public void Element_QuerySelector_Scope_Can_Match_Self()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
  <div id="host"><span id="child"></span></div>
  <script>
    (() => {
      var host = document.getElementById('host');
      document.body.setAttribute('data-result', host.querySelector(':scope').id);
    })();
  </script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("data-result=\"host\"", result);
    }

    [Fact]
    public void Element_Matches_And_Closest_Support_Scope()
    {
        const string html = """
<!DOCTYPE html>
<html><body>
  <div id="host" class="host"><span id="child"></span></div>
  <script>
    (() => {
      var child = document.getElementById('child');
      document.body.setAttribute('data-result', [
        child.matches(':scope'),
        child.closest(':scope').id,
        child.closest('.host').id
      ].join('|'));
    })();
  </script>
</body></html>
""";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("data-result=\"true|child|host\"", result);
    }
}
