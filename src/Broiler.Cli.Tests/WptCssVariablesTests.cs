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

    [Fact]
    public void VariableSubstitution_FirstLine_PseudoElement_ComputedStyle_Resolves()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
    #div1::first-line {
        color: var(--my-color);
        --my-color: rgb(0, 0, 255);
    }

    #div2::first-line {
        font-size: var(--my-font-size);
        --my-font-size: 25px;
    }

    #div3::first-line {
        font-weight: var(--my-font-weight);
        --my-font-weight: 900;
    }

    #div4::first-line {
        position: var(--my-position);
        --my-position: absolute;
    }
 </style>
</head>
<body>
<div id=""div1"">One two three four five six seven eight nine ten.</div>
<div id=""div2"">One two three four five six seven eight nine ten.</div>
<div id=""div3"">One two three four five six seven eight nine ten.</div>
<div id=""div4"">One two three four five six seven eight nine ten.</div>
<div id=""result""></div>
<script>
var r = [];
for (const id of ['div1', 'div2', 'div3', 'div4']) {
  const cs = window.getComputedStyle(document.getElementById(id), ':first-line');
  r.push(id + '=' + (cs.getPropertyValue('color') || '') + '|' + (cs.getPropertyValue('font-size') || '') + '|' + (cs.getPropertyValue('font-weight') || '') + '|' + (cs.getPropertyValue('position') || ''));
}
document.getElementById('result').textContent = r.join(';');
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("div1=rgb(0, 0, 255)|16px|normal|static", result);
        Assert.Contains("div2=rgb(0, 0, 0)|25px|normal|static", result);
        Assert.Contains("div3=rgb(0, 0, 0)|16px|900|static", result);
        Assert.Contains("div4=rgb(0, 0, 0)|16px|normal|static", result);
    }

    [Fact]
    public void VariableSubstitution_FirstLetter_PseudoElement_ComputedStyle_Resolves()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
    #div1::first-letter {
        color: var(--my-color);
        --my-color: rgb(0, 0, 255);
    }

    #div2::first-letter {
        font-size: var(--my-font-size);
        --my-font-size: 25px;
    }

    #div3::first-letter {
        font-weight: var(--my-font-weight);
        --my-font-weight: 900;
    }

    #div4::first-letter {
        position: var(--my-position);
        --my-position: absolute;
    }
</style>
</head>
<body>
<div id=""div1"">Alpha</div>
<div id=""div2"">Bravo</div>
<div id=""div3"">Charlie</div>
<div id=""div4"">Delta</div>
<div id=""result""></div>
<script>
var r = [];
for (const id of ['div1', 'div2', 'div3', 'div4']) {
  const cs = window.getComputedStyle(document.getElementById(id), ':first-letter');
  r.push(id + '=' + (cs.getPropertyValue('color') || '') + '|' + (cs.getPropertyValue('font-size') || '') + '|' + (cs.getPropertyValue('font-weight') || '') + '|' + (cs.getPropertyValue('position') || ''));
}
document.getElementById('result').textContent = r.join(';');
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("div1=rgb(0, 0, 255)|16px|normal|static", result);
        Assert.Contains("div2=rgb(0, 0, 0)|25px|normal|static", result);
        Assert.Contains("div3=rgb(0, 0, 0)|16px|900|static", result);
        Assert.Contains("div4=rgb(0, 0, 0)|16px|normal|static", result);
    }
}
