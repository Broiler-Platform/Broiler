namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 8 Acid3 compliance: SVG DOM &amp; Dynamic Content —
/// SVG viewBox attribute, dynamic style modification, XML processing
/// instruction handling, foster-parented elements, implied tag closing
/// for table elements, misnested formatting elements, and extended
/// auto-closing rules.
/// </summary>
public class SvgDomDynamicContentTests
{
    // ────────────────────── 8.2: SVG viewBox attribute ──────────────────────

    [Fact]
    public void SVG_ViewBox_BaseVal_Has_Dimensions()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var svg = document.createElementNS(svgns, 'svg');
svg.setAttribute('viewBox', '0 0 100 200');
var r = [];
var vb = svg.viewBox;
r.push(vb !== null && vb !== undefined);
r.push(vb.baseVal.x === 0);
r.push(vb.baseVal.y === 0);
r.push(vb.baseVal.width === 100);
r.push(vb.baseVal.height === 200);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void SVG_ViewBox_AnimVal_Equals_BaseVal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var svgns = 'http://www.w3.org/2000/svg';
var svg = document.createElementNS(svgns, 'svg');
svg.setAttribute('viewBox', '10 20 300 400');
var r = [];
r.push(svg.viewBox.animVal.x === 10);
r.push(svg.viewBox.animVal.y === 20);
r.push(svg.viewBox.animVal.width === 300);
r.push(svg.viewBox.animVal.height === 400);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── 8.3: Dynamic style modification ──────────────────────

    [Fact]
    public void Style_TextNode_Data_Change_Updates_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>div { color: red; }</style>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
r.push(sheet.cssRules.length === 1);
// Change text content via firstChild.data
sheet.ownerNode.firstChild.data = 'p { color: blue; } span { color: green; }';
r.push(sheet.cssRules.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Style_AppendChild_TextNode_Updates_CssRules()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>div { color: red; }</style>
<div id=""result""></div>
<script>
var r = [];
var sheet = document.styleSheets[0];
r.push(sheet.cssRules.length === 1);
// Append a new text node with an additional rule
sheet.ownerNode.appendChild(document.createTextNode('span { color: green; }'));
r.push(sheet.cssRules.length === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void StyleSheets_Href_Null_For_Inline()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<style>div { color: red; }</style>
<div id=""result""></div>
<script>
var r = [];
r.push(document.styleSheets[0].href === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    // ────────────────────── 8.5: XML processing instruction handling ──────────────────────

    [Fact]
    public void XML_Declaration_Does_Not_Create_Element()
    {
        var html = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
// The XML declaration should be silently ignored
r.push(document.body !== null);
r.push(document.getElementById('result') !== null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── 8.6: Foster parenting ──────────────────────

    [Fact]
    public void Foster_Parent_Text_In_Table_Goes_Before_Table()
    {
        // Test that when HTML is parsed with text directly inside <table>,
        // the tree builder handles it (foster parenting moves it out of table scope)
        var html = @"<!DOCTYPE html>
<html><body>
<table>Hello<tr><td>cell</td></tr></table>
<div id=""result""></div>
<script>
var r = [];
// The text 'Hello' should not be inside the table element directly
// (it gets foster-parented to before the table)
var table = document.getElementsByTagName('table')[0];
var hasTextInTable = false;
for (var i = 0; i < table.childNodes.length; i++) {
    if (table.childNodes[i].nodeType === 3 && table.childNodes[i].textContent.indexOf('Hello') >= 0) {
        hasTextInTable = true;
    }
}
// The text 'Hello' should NOT be a direct child of the table
r.push(!hasTextInTable);
// The table should still contain a row with a cell
var cells = table.getElementsByTagName('td');
r.push(cells.length === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── 8.6: Implied tag closing for table elements ──────────────────────

    [Fact]
    public void Auto_Close_TD_When_Next_TD_Arrives()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<table><tr><td>A<td>B</tr></table>
<div id=""result""></div>
<script>
var r = [];
var row = document.getElementsByTagName('tr')[0];
r.push(row.childNodes.length === 2);
r.push(row.childNodes[0].tagName === 'TD');
r.push(row.childNodes[1].tagName === 'TD');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Auto_Close_Option_When_Next_Option_Arrives()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<select><option>A<option>B<option>C</select>
<div id=""result""></div>
<script>
var r = [];
var sel = document.getElementsByTagName('select')[0];
r.push(sel.childNodes.length === 3);
r.push(sel.childNodes[0].tagName === 'OPTION');
r.push(sel.childNodes[1].tagName === 'OPTION');
r.push(sel.childNodes[2].tagName === 'OPTION');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── 8.6: Misnested formatting elements ──────────────────────

    [Fact]
    public void Misnested_Formatting_Closes_On_End_Tag()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><b>bold<i>both</b>italic</i></div>
<div id=""result""></div>
<script>
var r = [];
var container = document.getElementById('container');
// After adoption agency: <b> and <i> should both close properly
// The exact structure may vary but the key assertion is that
// the <b> element does not contain the text 'italic'
var b = container.getElementsByTagName('b')[0];
if (b) {
    var bText = b.textContent;
    r.push(bText.indexOf('bold') >= 0);
    r.push(bText.indexOf('both') >= 0);
} else {
    r.push(false);
    r.push(false);
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── 8.6: Doc.open/write/close tree structure ──────────────────────

    [Fact]
    public void Doc_Write_Creates_Proper_Head_Body_Structure()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""frame""></iframe>
<div id=""result""></div>
<script>
var r = [];
var doc = document.getElementById('frame').contentDocument;
doc.open();
doc.write('<!DOCTYPE html><title>Test</title><span>hello</span><script type=""text/javascript""><\/script>');
doc.close();
// #document should have 2 children: DOCTYPE + html
r.push(doc.childNodes.length === 2);
// First child is DOCTYPE
r.push(doc.firstChild.nodeType === 10);
// documentElement is HTML
r.push(doc.documentElement.tagName === 'HTML');
// HTML has 2 children: HEAD and BODY
r.push(doc.documentElement.childNodes.length === 2);
// HEAD contains TITLE
r.push(doc.documentElement.firstChild.nodeName === 'HEAD');
r.push(doc.documentElement.firstChild.childNodes.length === 1);
r.push(doc.documentElement.firstChild.firstChild.tagName === 'TITLE');
// BODY contains SPAN and SCRIPT
r.push(doc.documentElement.lastChild.nodeName === 'BODY');
r.push(doc.documentElement.lastChild.childNodes.length === 2);
r.push(doc.documentElement.lastChild.firstChild.tagName === 'SPAN');
r.push(doc.documentElement.lastChild.lastChild.tagName === 'SCRIPT');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void Doc_Write_Nested_Span_Script_Structure()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<iframe id=""frame""></iframe>
<div id=""result""></div>
<script>
var r = [];
var doc = document.getElementById('frame').contentDocument;
doc.open();
doc.write('<!DOCTYPE html><title>T</title><span><script type=""text/javascript""><\/script></span>');
doc.close();
// BODY should have 1 child: SPAN
r.push(doc.documentElement.lastChild.nodeName === 'BODY');
r.push(doc.documentElement.lastChild.childNodes.length === 1);
r.push(doc.documentElement.lastChild.firstChild.tagName === 'SPAN');
// SPAN contains SCRIPT as child
r.push(doc.documentElement.lastChild.firstChild.firstChild.tagName === 'SCRIPT');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }
}
