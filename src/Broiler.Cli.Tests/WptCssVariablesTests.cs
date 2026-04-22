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

    [Fact]
    public void VariableSubstitution_MissingClosingNestedFallback_ComputedStyle_Resolves()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
#target {
  box-shadow: var(--token-outer, 10px 10px 10px 10px rgb(245, 245, 245), 0px 0px 4px 2px var(--token-inner, rgb(1, 255, 148))
}
</style>
</head>
<body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var value = window.getComputedStyle(document.getElementById('target')).getPropertyValue('box-shadow') || '';
document.getElementById('result').textContent =
  value.indexOf('rgb(245, 245, 245) 10px 10px 10px 10px') >= 0 &&
  value.indexOf('rgb(1, 255, 148) 0px 0px 4px 2px') >= 0
    ? 'PASS'
    : 'FAIL:' + value;
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("PASS", result);
    }

    [Fact]
    public void VariableSubstitution_CssWideKeywords_On_CustomProperties_Resolve()
    {
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
  body {
    --shared-token: lightgreen;
    --registered-inherits: tomato;
    --registered-no-inherit: tomato;
  }

  @property --registered-inherits {
    syntax: '<color>';
    initial-value: orange;
    inherits: true;
  }

  @property --registered-no-inherit {
    syntax: '<color>';
    initial-value: lightgreen;
    inherits: false;
  }

  #initial { background: var(--initial-token, hotpink); --initial-token: initial; }
  #inherit { background: var(--shared-token, hotpink); --shared-token: inherit; }
  #unset { background: var(--shared-token, hotpink); --shared-token: unset; }
  #revert { background: var(--shared-token, hotpink); --shared-token: revert; }
  #registeredInitial { background: var(--registered-no-inherit); --registered-no-inherit: initial; }
  #registeredInherit { background: var(--registered-inherits); --registered-inherits: inherit; }
</style>
</head>
<body>
  <div id=""initial""></div>
  <div id=""inherit""></div>
  <div id=""unset""></div>
  <div id=""revert""></div>
  <div id=""registeredInitial""></div>
  <div id=""registeredInherit""></div>
  <div id=""fallbackInitial""></div>
  <div id=""result""></div>
<script>
function colorMatches(actual) {
  var expected = Array.prototype.slice.call(arguments, 1);
  actual = (actual || '').trim();
  return expected.indexOf(actual) >= 0;
}

var checks = [];
checks.push(colorMatches(getComputedStyle(document.getElementById('initial')).getPropertyValue('background-color'), 'hotpink', 'rgb(255, 105, 180)'));
checks.push(colorMatches(getComputedStyle(document.getElementById('inherit')).getPropertyValue('background-color'), 'lightgreen', 'rgb(144, 238, 144)'));
checks.push(colorMatches(getComputedStyle(document.getElementById('unset')).getPropertyValue('background-color'), 'lightgreen', 'rgb(144, 238, 144)'));
checks.push(colorMatches(getComputedStyle(document.getElementById('revert')).getPropertyValue('background-color'), 'lightgreen', 'rgb(144, 238, 144)'));
checks.push(colorMatches(getComputedStyle(document.getElementById('registeredInitial')).getPropertyValue('background-color'), 'lightgreen', 'rgb(144, 238, 144)'));
checks.push(colorMatches(getComputedStyle(document.getElementById('registeredInherit')).getPropertyValue('background-color'), 'tomato', 'rgb(255, 99, 71)'));

var outer = document.createElement('div');
outer.style.color = 'transparent';
outer.style.border = '10px solid';
var inner = document.createElement('div');
inner.id = 'fallbackInitial';
inner.style.color = 'var(--unknown, initial)';
inner.style.borderWidth = '10px';
inner.style.borderStyle = 'var(--unknown, inherit)';
outer.appendChild(inner);
document.body.appendChild(outer);

var initialStyle = getComputedStyle(inner);
checks.push(colorMatches(initialStyle.getPropertyValue('color'), 'rgb(0, 0, 0)') &&
            (initialStyle.getPropertyValue('border-top-style') || '').trim() === 'solid');
document.getElementById('result').textContent = checks.every(Boolean) ? 'PASS' : 'FAIL';
</script>
</body>
</html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("PASS", result);
    }
}
