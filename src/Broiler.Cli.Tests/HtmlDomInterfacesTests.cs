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

    [Fact]
    public void InnerText_Returns_Descendant_Text_Content()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"">Hello<span>World</span></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
document.getElementById('result').textContent = d.innerText;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("HelloWorld", result);
    }

    [Fact]
    public void OuterText_Returns_Descendant_Text_Content()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"">Hello<span>World</span></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
document.getElementById('result').textContent = d.outerText;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("HelloWorld", result);
    }

    [Fact]
    public void OuterHtml_Returns_Serialized_Element_Markup()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" class=""foo""><span>World</span></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
document.getElementById('result').textContent =
    d.outerHTML === '<div id=""d"" class=""foo""><span>World</span></div>';
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true", result);
    }

    [Fact]
    public void OuterHtml_Setter_Replaces_Element_In_Parent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""><div id=""target"">old</div></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var target = document.getElementById('target');
target.outerHTML = '<span id=""replacement"">new</span><em>tail</em>';
var r = [];
r.push(document.getElementById('target') === null);
r.push(host.childNodes.length === 2);
r.push(host.firstChild.tagName.toLowerCase() === 'span');
r.push(host.lastChild.tagName.toLowerCase() === 'em');
r.push(document.getElementById('replacement').textContent === 'new');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Append_Adds_Nodes_And_Text_In_Order()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var strong = document.createElement('strong');
strong.textContent = 'B';
host.append('A', strong, 'C');
document.getElementById('result').textContent =
    host.firstChild.data + '|' +
    host.childNodes[1].tagName.toLowerCase() + '|' +
    host.childNodes[2].data + '|' +
    host.childNodes.length;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("A|strong|C|3", result);
    }

    [Fact]
    public void Prepend_Adds_Nodes_Before_Existing_Children()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""><span>tail</span></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var strong = document.createElement('strong');
strong.textContent = 'mid';
host.prepend('head', strong);
document.getElementById('result').textContent =
    host.firstChild.data + '|' +
    host.childNodes[1].tagName.toLowerCase() + '|' +
    host.lastChild.tagName.toLowerCase() + '|' +
    host.childNodes.length;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("head|strong|span|3", result);
    }

    [Fact]
    public void Append_Unpacks_DocumentFragment_Children()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var fragment = document.createDocumentFragment();
var first = document.createElement('span');
first.textContent = 'one';
var second = document.createElement('span');
second.textContent = 'two';
fragment.appendChild(first);
fragment.appendChild(second);
host.append(fragment);
document.getElementById('result').textContent =
    host.childNodes.length + '|' +
    fragment.childNodes.length + '|' +
    host.firstChild.tagName.toLowerCase() + '|' +
    host.lastChild.tagName.toLowerCase();
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("2|0|span|span", result);
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
    public void Element_HasAttributes_Reflects_Attribute_Presence()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""with-attrs"" class=""foo""></div>
<div></div>
<div id=""result""></div>
<script>
var withAttrs = document.getElementById('with-attrs');
var withoutAttrs = document.getElementsByTagName('div')[1];
var r = [];
r.push(withAttrs.hasAttributes() === true);
r.push(withoutAttrs.hasAttributes() === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Element_GetAttributeNames_Returns_All_Attribute_Names()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" class=""foo"" title=""bar""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var names = d.getAttributeNames();
var r = [];
r.push(Array.isArray(names));
r.push(names.indexOf('id') >= 0);
r.push(names.indexOf('class') >= 0);
r.push(names.indexOf('title') >= 0);
r.push(names.length >= 3);
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
    public void Document_CreateAttribute_Creates_Standalone_Attr_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var attr = document.createAttribute('DATA-X');
var r = [];
r.push(attr !== null);
r.push(attr.nodeType === 2);
r.push(attr.name === 'data-x');
r.push(attr.value === '');
r.push(attr.ownerElement === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Document_CreateAttributeNS_Creates_Namespaced_Attr_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var attr = document.createAttributeNS('http://example.com/ns', 'ex:data-x');
var r = [];
r.push(attr.name === 'ex:data-x');
r.push(attr.localName === 'data-x');
r.push(attr.prefix === 'ex');
r.push(attr.namespaceURI === 'http://example.com/ns');
r.push(attr.ownerElement === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Document_CreateAttribute_Can_Be_Attached_With_SetAttributeNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attr = document.createAttribute('data-x');
attr.value = '42';
var old = d.setAttributeNode(attr);
var attached = d.getAttributeNode('data-x');
var r = [];
r.push(old === null);
r.push(d.getAttribute('data-x') === '42');
r.push(attached !== null);
r.push(attached.ownerElement === d);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Element_GetAttributeNodeNS_Returns_Namespaced_Attr_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var ns = 'http://www.w3.org/1999/xlink';
var el = document.createElement('div');
el.setAttributeNS(ns, 'xlink:href', 'http://example.com');
var attr = el.getAttributeNodeNS(ns, 'href');
var r = [];
r.push(attr !== null);
r.push(attr.name === 'xlink:href');
r.push(attr.localName === 'href');
r.push(attr.namespaceURI === ns);
r.push(attr.ownerElement === el);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Element_SetAttributeNodeNS_Adds_And_Replaces_Namespaced_Attr()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var ns = 'http://www.w3.org/1999/xlink';
var el = document.createElement('div');
var first = document.createAttributeNS(ns, 'xlink:href');
first.value = 'http://example.com/a';
var old1 = el.setAttributeNodeNS(first);
var second = document.createAttributeNS(ns, 'xlink:href');
second.value = 'http://example.com/b';
var old2 = el.setAttributeNodeNS(second);
var r = [];
r.push(old1 === null);
r.push(old2 !== null);
r.push(old2.value === 'http://example.com/a');
r.push(el.getAttributeNS(ns, 'href') === 'http://example.com/b');
r.push(el.getAttributeNodeNS(ns, 'href').namespaceURI === ns);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Element_RemoveAttributeNodeNS_Removes_And_Returns_Namespaced_Attr()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var ns = 'http://www.w3.org/1999/xlink';
var el = document.createElement('div');
el.setAttributeNS(ns, 'xlink:href', 'http://example.com');
var attr = el.getAttributeNodeNS(ns, 'href');
var removed = el.removeAttributeNodeNS(attr);
var r = [];
r.push(removed !== null);
r.push(removed.value === 'http://example.com');
r.push(removed.namespaceURI === ns);
r.push(el.getAttributeNS(ns, 'href') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void NamedNodeMap_Namespace_Aware_Methods_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var ns = 'http://www.w3.org/1999/xlink';
var el = document.createElement('div');
var attrs = el.attributes;
var attr = document.createAttributeNS(ns, 'xlink:href');
attr.value = 'http://example.com';
var old = attrs.setNamedItemNS(attr);
var fetched = attrs.getNamedItemNS(ns, 'href');
var beforeRemove = el.getAttributeNS(ns, 'href');
var removed = attrs.removeNamedItemNS(ns, 'href');
var r = [];
r.push(old === null);
r.push(fetched !== null);
r.push(fetched.namespaceURI === ns);
r.push(beforeRemove === 'http://example.com');
r.push(removed.value === 'http://example.com');
r.push(el.getAttributeNS(ns, 'href') === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void Element_GetAttributeNode_Returns_Attr_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" class=""x""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attr = d.getAttributeNode('class');
var r = [];
r.push(attr !== null);
r.push(attr.nodeType === 2);
r.push(attr.nodeName === 'class');
r.push(attr.ownerElement === d);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Element_SetAttributeNode_Replaces_And_Returns_Old_Attr()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" class=""old""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attr = d.getAttributeNode('class');
attr.value = 'new';
var old = d.setAttributeNode(attr);
var r = [];
r.push(old !== null);
r.push(old.value === 'old');
r.push(d.getAttribute('class') === 'new');
r.push(d.className === 'new');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Element_RemoveAttributeNode_Removes_And_Returns_Attr()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" title=""hello""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var attr = d.getAttributeNode('title');
var removed = d.removeAttributeNode(attr);
var r = [];
r.push(removed !== null);
r.push(removed.value === 'hello');
r.push(d.getAttribute('title') === null);
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

    [Fact]
    public void Element_ToggleAttribute_Adds_Then_Removes_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var r = [];
r.push(d.toggleAttribute('hidden') === true);
r.push(d.hasAttribute('hidden') === true);
r.push(d.getAttribute('hidden') === '');
r.push(d.toggleAttribute('hidden') === false);
r.push(d.hasAttribute('hidden') === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Element_ToggleAttribute_With_Force_True_Adds_Once_And_Returns_True()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var r = [];
r.push(d.toggleAttribute('data-armed', true) === true);
r.push(d.toggleAttribute('data-armed', true) === true);
r.push(d.getAttribute('data-armed') === '');
r.push(d.hasAttribute('data-armed') === true);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Element_ToggleAttribute_With_Force_False_Removes_And_Returns_False()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" title=""hello""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var r = [];
r.push(d.toggleAttribute('title', false) === false);
r.push(d.getAttribute('title') === null);
r.push(d.toggleAttribute('title', false) === false);
r.push(d.hasAttribute('title') === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void Node_CompareDocumentPosition_Reports_Self_Ancestor_And_Sibling_Order()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><span id=""child1""></span><span id=""child2""></span></div>
<div id=""result""></div>
<script>
var parent = document.getElementById('parent');
var child1 = document.getElementById('child1');
var child2 = document.getElementById('child2');
var r = [];
r.push(parent.compareDocumentPosition(parent) === 0);
r.push((parent.compareDocumentPosition(child1) & 16) === 16);
r.push((parent.compareDocumentPosition(child1) & 4) === 4);
r.push((child1.compareDocumentPosition(parent) & 8) === 8);
r.push((child1.compareDocumentPosition(parent) & 2) === 2);
r.push((child1.compareDocumentPosition(child2) & 4) === 4);
r.push((child2.compareDocumentPosition(child1) & 2) === 2);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    [Fact]
    public void Node_CompareDocumentPosition_Sets_Disconnected_Bit_For_Separate_Trees()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var first = document.createElement('div');
var second = document.createElement('div');
var r = [];
r.push((first.compareDocumentPosition(second) & 1) === 1);
r.push((second.compareDocumentPosition(first) & 1) === 1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Node_IsSameNode_Identifies_Identical_References()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d1 = document.getElementById('d');
var d2 = document.getElementById('d');
var other = document.createElement('div');
var r = [];
r.push(d1.isSameNode(d1) === true);
r.push(d1.isSameNode(d2) === true);
r.push(d1.isSameNode(other) === false);
r.push(other.isSameNode(other) === true);
r.push(d1.isSameNode(null) === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true", result);
    }

    [Fact]
    public void Node_Normalize_Merges_Adjacent_Text_Nodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
host.appendChild(document.createTextNode('hello'));
host.appendChild(document.createTextNode(' '));
host.appendChild(document.createTextNode('world'));
var r = [];
r.push(host.childNodes.length === 3);
host.normalize();
r.push(host.childNodes.length === 1);
r.push(host.firstChild.data === 'hello world');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Node_Normalize_Removes_Empty_Text_Nodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
host.appendChild(document.createTextNode(''));
host.appendChild(document.createTextNode('text'));
host.appendChild(document.createTextNode(''));
var r = [];
r.push(host.childNodes.length === 3);
host.normalize();
r.push(host.childNodes.length === 1);
r.push(host.firstChild.data === 'text');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Node_Normalize_Recursively_Normalizes_Descendants()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""><span id=""child""></span></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var child = document.getElementById('child');
child.appendChild(document.createTextNode('a'));
child.appendChild(document.createTextNode('b'));
var r = [];
r.push(child.childNodes.length === 2);
host.normalize();
r.push(child.childNodes.length === 1);
r.push(child.firstChild.data === 'ab');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
    }

    [Fact]
    public void Node_IsEqualNode_Matches_Equal_Structure()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""host""><span class=""a"">text</span></div>
<div id=""result""></div>
<script>
var host = document.getElementById('host');
var clone = host.cloneNode(true);
var r = [];
r.push(host.isEqualNode(clone) === true);
r.push(clone.isEqualNode(host) === true);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Node_IsEqualNode_Detects_Attribute_And_Text_Differences()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var first = document.createElement('div');
first.setAttribute('data-x', '1');
first.appendChild(document.createTextNode('same'));
var second = first.cloneNode(true);
second.setAttribute('data-x', '2');
var third = first.cloneNode(true);
third.firstChild.data = 'different';
var r = [];
r.push(first.isEqualNode(second) === false);
r.push(first.isEqualNode(third) === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Node_IsEqualNode_Detects_Nested_Descendant_Differences()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var first = document.createElement('div');
var firstChild = document.createElement('span');
firstChild.appendChild(document.createTextNode('nested'));
first.appendChild(firstChild);
var second = first.cloneNode(true);
second.firstChild.appendChild(document.createElement('em'));
var r = [];
r.push(first.isEqualNode(second) === false);
r.push(second.isEqualNode(first) === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void Node_IsEqualNode_Returns_False_For_Null_And_True_For_Equal_Text_Nodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var first = document.createTextNode('abc');
var second = document.createTextNode('abc');
var third = document.createTextNode('xyz');
var r = [];
r.push(first.isEqualNode(null) === false);
r.push(first.isEqualNode(second) === true);
r.push(first.isEqualNode(third) === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true", result);
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

    [Fact]
    public void HTMLElement_Hidden_Property_Reflects_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d""></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var r = [];
r.push(d.hidden === false);
d.hidden = true;
r.push(d.hidden === true);
r.push(d.hasAttribute('hidden') === true);
r.push(d.getAttribute('hidden') === '');
d.hidden = false;
r.push(d.hidden === false);
r.push(d.hasAttribute('hidden') === false);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void HTMLElement_Hidden_Property_Uses_Existing_Hidden_Attribute_And_Ua_Display_None()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d"" hidden>hello</div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var r = [];
r.push(d.hidden === true);
r.push(d.hasAttribute('hidden') === true);
r.push(d.getAttribute('hidden') === '');
d.hidden = false;
r.push(d.hasAttribute('hidden') === false);
r.push(d.hidden === false);
d.hidden = true;
r.push(d.hasAttribute('hidden') === true);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void HTMLElement_TabIndex_Property_Reflects_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d1""></div>
<div id=""d2"" tabindex=""5""></div>
<div id=""result""></div>
<script>
var d1 = document.getElementById('d1');
var d2 = document.getElementById('d2');
var r = [];
r.push(d1.tabIndex === -1);
r.push(d2.tabIndex === 5);
d1.tabIndex = 3;
r.push(d1.getAttribute('tabindex') === '3');
r.push(d1.tabIndex === 3);
d2.tabIndex = -1;
r.push(d2.getAttribute('tabindex') === '-1');
r.push(d2.tabIndex === -1);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    [Fact]
    public void HTMLElement_Lang_Property_Reflects_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""d1""></div>
<div id=""d2"" lang=""fr-CA""></div>
<div id=""result""></div>
<script>
var d1 = document.getElementById('d1');
var d2 = document.getElementById('d2');
var r = [];
r.push(d1.lang === '');
r.push(d2.lang === 'fr-CA');
d1.lang = 'en-US';
r.push(d1.getAttribute('lang') === 'en-US');
r.push(d1.lang === 'en-US');
d2.lang = '';
r.push(d2.getAttribute('lang') === '');
r.push(d2.lang === '');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true,true,true", result);
    }
}
