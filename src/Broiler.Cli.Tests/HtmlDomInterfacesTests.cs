namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 7 Acid3 compliance: HTML DOM Interfaces —
/// form element state preservation (checkbox/radio checked state across
/// removeChild/appendChild/cloneNode), className whitespace preservation,
/// NamedNodeMap (element.attributes), Attr nodes, and area element properties.
/// </summary>
public class HtmlDomInterfacesTests
{
    // ────────────────────── 7.1: Form element state preservation ──────────────────────

    [Fact]
    public void Checkbox_Checked_State_Survives_RemoveChild_AppendChild()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><input id=""cb"" type=""checkbox"" /></div>
<div id=""target""></div>
<div id=""result""></div>
<script>
var r = [];
var cb = document.getElementById('cb');
cb.checked = true;
r.push(cb.checked);
var container = document.getElementById('container');
container.removeChild(cb);
r.push(cb.checked);
var target = document.getElementById('target');
target.appendChild(cb);
r.push(cb.checked);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Radio_Checked_State_Survives_CloneNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<form>
<input id=""r1"" type=""radio"" name=""grp"" />
</form>
<div id=""result""></div>
<script>
var r = [];
var r1 = document.getElementById('r1');
r1.checked = true;
r.push(r1.checked);
var clone = r1.cloneNode(false);
r.push(clone.checked);
r.push('' + clone.getAttribute('type'));
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,radio", result);
    }

    // ────────────────────── 7.2: className whitespace preservation ──────────────────────

    [Fact]
    public void ClassName_Whitespace_Preserved_In_GetAttribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
d.className = '  a  b  ';
var r = [];
r.push(d.getAttribute('class') === '  a  b  ');
r.push(d.className === '  a  b  ');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    // ────────────────────── 7.3: Attribute node interface ──────────────────────

    [Fact]
    public void Attributes_Length_And_GetNamedItem()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" class=""foo"" title=""bar""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attrs = d.attributes;
var r = [];
r.push(attrs.length >= 3);
var idAttr = attrs.getNamedItem('id');
r.push(idAttr.name === 'id');
r.push(idAttr.value === 'd');
r.push(idAttr.specified === true);
r.push(idAttr.nodeType === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Attributes_SetNamedItem_Adds_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attrs = d.attributes;
var newAttr = { name: 'data-x', value: '42' };
attrs.setNamedItem(newAttr);
var r = [];
r.push(d.getAttribute('data-x') === '42');
var fetched = attrs.getNamedItem('data-x');
r.push(fetched.name === 'data-x');
r.push(fetched.value === '42');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Attributes_RemoveNamedItem_Removes_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" title=""hello""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attrs = d.attributes;
var r = [];
r.push(d.getAttribute('title') === 'hello');
var removed = attrs.removeNamedItem('title');
r.push(removed.name === 'title');
r.push(removed.value === 'hello');
r.push(d.getAttribute('title') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Attr_Node_OwnerElement_Points_Back()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" class=""x""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attr = d.attributes.getNamedItem('class');
var r = [];
r.push(attr.ownerElement === d);
r.push(attr.nodeName === 'class');
r.push(attr.specified === true);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Attributes_Item_Returns_By_Index()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.setAttribute('alpha', '1');
d.setAttribute('beta', '2');
var attrs = d.attributes;
var r = [];
r.push(attrs.item(0) !== null);
r.push(attrs.item(0).nodeType === 2);
r.push(attrs.length >= 2);
r.push(attrs.item(999) === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    // ────────────────────── 7.4: <area> element properties ──────────────────────

    [Fact]
    public void Area_Element_Shape_Coords_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var area = document.createElement('area');
area.shape = 'rect';
area.coords = '0,0,100,100';
area.alt = 'test area';
area.target = '_blank';
var r = [];
r.push(area.shape === 'rect');
r.push(area.coords === '0,0,100,100');
r.push(area.alt === 'test area');
r.push(area.target === '_blank');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Area_Element_Href_Property_With_Resolution()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var area = document.createElement('area');
area.href = '/page';
var r = [];
r.push(area.href.indexOf('/page') !== -1);
area.href = 'https://example.com/test';
r.push(area.href === 'https://example.com/test');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }
}
