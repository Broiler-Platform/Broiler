namespace Broiler.Cli.Tests;

/// <summary>
/// CSSOM <c>disabled</c> stylesheet semantics (issue #1458, <c>css/cssom/HTMLLinkElement-disabled-*</c>):
/// a disabled sheet does not apply to the cascade (CSSOM §2.3), exposed through the
/// <c>CSSStyleSheet.disabled</c> IDL attribute and the <c>HTMLLinkElement.disabled</c> content-attribute
/// reflection.
/// </summary>
public class StyleSheetDisabledTests
{
    [Fact]
    public void StyleSheetDisabledSetter_RemovesRulesFromCascade_ButKeepsSheetInCollection()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>#t { color: rgb(0, 128, 0); }</style>
</head><body>
<div id=""t"">x</div>
<div id=""result""></div>
<script>
var r = [];
var t = document.getElementById('t');
r.push(window.getComputedStyle(t).color);          // green — the sheet applies
var sheet = document.styleSheets[0];
r.push(String(sheet.disabled));                     // false initially
sheet.disabled = true;                              // CSSOM: stop applying
r.push(String(sheet.disabled));                     // true
r.push(window.getComputedStyle(t).color !== 'rgb(0, 128, 0)'); // no longer green
r.push(String(document.styleSheets.length));        // a disabled <style> stays in the collection
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("rgb(0, 128, 0)|false|true|true|1", result);
    }

    [Fact]
    public void StyleSheetDisabled_ReEnabling_RestoresTheCascade()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>#t { color: rgb(0, 128, 0); }</style>
</head><body>
<div id=""t"">x</div>
<div id=""result""></div>
<script>
var r = [];
var t = document.getElementById('t');
var sheet = document.styleSheets[0];
sheet.disabled = true;
r.push(window.getComputedStyle(t).color !== 'rgb(0, 128, 0)'); // disabled: not green
sheet.disabled = false;                                        // re-enable
r.push(window.getComputedStyle(t).color);                      // green again
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|rgb(0, 128, 0)", result);
    }

    [Fact]
    public void HtmlLinkElementDisabled_ReflectsAndTogglesTheContentAttribute()
    {
        // HTMLLinkElement.disabled reflects the `disabled` content attribute: getting reads it,
        // setting adds/removes it. (css/cssom/HTMLLinkElement-disabled-001/003.)
        var html = @"<!DOCTYPE html>
<html><head>
<link id=""l"" rel=""stylesheet"" disabled href=""sheet.css"">
</head><body>
<div id=""result""></div>
<script>
var r = [];
var l = document.getElementById('l');
r.push(String(l.disabled));                 // true — from the content attribute
l.disabled = false;                         // clears the content attribute
r.push(String(l.disabled));                 // false
r.push(String(l.hasAttribute('disabled'))); // false
l.disabled = true;                          // re-adds the content attribute
r.push(String(l.disabled));                 // true
r.push(String(l.hasAttribute('disabled'))); // true
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true|false|false|true|true", result);
    }
}
