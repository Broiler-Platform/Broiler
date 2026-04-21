namespace Broiler.Cli.Tests;

public class WptCssVariablesTests
{
    [Fact]
    public void VariableSubstitution_Shorthands_Resolve_From_Same_Block()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""fontTarget"" style=""--font: 24px/2 serif; font: var(--font);"">A</div>
<div id=""borderTarget"" style=""--border: 3px dotted red; border-left: var(--border);""></div>
<div id=""marginTarget"" style=""--margin: 3px 5px 7px 11px; margin: var(--margin);""></div>
<div id=""result""></div>
<script>
var fontTarget = document.getElementById('fontTarget');
var borderTarget = document.getElementById('borderTarget');
var marginTarget = document.getElementById('marginTarget');
var fontStyle = window.getComputedStyle(fontTarget);
var borderStyle = window.getComputedStyle(borderTarget);
var marginStyle = window.getComputedStyle(marginTarget);
document.getElementById('result').textContent =
  'FONT=' + (fontStyle.getPropertyValue('font-size') || fontStyle.fontSize || '') +
  ';LINE=' + (fontStyle.getPropertyValue('line-height') || fontStyle.lineHeight || '') +
  ';BORDER=' + (borderStyle.getPropertyValue('border-left-width') || '') + '|' +
                (borderStyle.getPropertyValue('border-left-style') || '') + '|' +
                (borderStyle.getPropertyValue('border-left-color') || '') +
  ';MARGIN=' + (marginStyle.getPropertyValue('margin-top') || '') + '|' +
                (marginStyle.getPropertyValue('margin-right') || '') + '|' +
                (marginStyle.getPropertyValue('margin-bottom') || '') + '|' +
                (marginStyle.getPropertyValue('margin-left') || '');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("FONT=24px", result);
        Assert.Contains("LINE=2", result);
        Assert.Contains("BORDER=3px|dotted|red", result);
        Assert.Contains("MARGIN=3px|5px|7px|11px", result);
    }
}
