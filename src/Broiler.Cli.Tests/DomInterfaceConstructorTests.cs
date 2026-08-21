namespace Broiler.Cli.Tests;

/// <summary>
/// The DOM interface-constructor globals — <c>Element</c>, <c>HTMLElement</c>,
/// <c>HTMLUnknownElement</c>, <c>Document</c>, <c>DocumentFragment</c>, <c>CharacterData</c>,
/// <c>Text</c>, <c>Comment</c>, <c>Attr</c> — and the <c>instanceof</c> answers they give
/// (see <c>DomBridge.RegisterDomInterfaceConstructors</c>).
///
/// A bridge DOM object is a plain JSObject with <c>Object.prototype</c> for a prototype, so the
/// ordinary <c>instanceof</c> prototype walk can never match; each constructor answers from the
/// object's own <c>nodeType</c>/<c>namespaceURI</c>/<c>tagName</c> through <c>@@hasInstance</c>.
/// Before this, <c>element instanceof HTMLElement</c> threw
/// <c>ReferenceError: HTMLElement is not defined</c> — one of the four exceptions
/// html5test.com's feature probes hit.
/// </summary>
public class DomInterfaceConstructorTests
{
    private static string Run(string script)
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
" + script + @"
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
    }

    [Fact(Timeout = 600000)]
    public void Interface_Constructors_Are_Defined_As_Bare_Globals()
    {
        var result = Run(@"
var names = ['Node','Element','HTMLElement','HTMLUnknownElement','Document',
             'DocumentFragment','CharacterData','Text','Comment','Attr'];
for (var i = 0; i < names.length; i++) {
  r.push(typeof window[names[i]] === 'function');
}
// Reached by bare name too, not only through window.
r.push(typeof HTMLElement === 'function');
r.push(typeof Element === 'function');
");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Element_Is_Instance_Of_HTMLElement_Element_And_Node()
    {
        var result = Run(@"
var d = document.createElement('div');
r.push(d instanceof HTMLElement);
r.push(d instanceof Element);
r.push(d instanceof Node);
r.push(!(d instanceof HTMLUnknownElement));
r.push(!(d instanceof Text));
r.push(!(d instanceof Document));
");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    /// <summary>
    /// The html5test.com probe at <c>engine.js:228</c>:
    /// <c>element instanceof HTMLElement &amp;&amp; !(element instanceof HTMLUnknownElement)</c>
    /// over the HTML5 sectioning elements. Each has no dedicated interface of its own, so each is
    /// a plain HTMLElement and specifically *not* an HTMLUnknownElement.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Html5_Sectioning_Elements_Are_HTMLElement_And_Not_Unknown()
    {
        var result = Run(@"
var els = 'section nav article aside header footer main figure figcaption'.split(' ');
for (var i = 0; i < els.length; i++) {
  var el = document.createElement(els[i]);
  r.push(el instanceof HTMLElement && !(el instanceof HTMLUnknownElement));
}
");
        Assert.Contains("true,true,true,true,true,true,true,true,true", result);
    }

    /// <summary>
    /// HTMLUnknownElement is a subtype of HTMLElement, so an unknown element is an instance of
    /// both. A hyphenated name is a custom element, which the spec makes an HTMLElement rather
    /// than an unknown one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Unknown_And_Custom_Element_Names_Are_Distinguished()
    {
        var result = Run(@"
var unknown = document.createElement('foobar');
r.push(unknown instanceof HTMLElement);
r.push(unknown instanceof HTMLUnknownElement);
var custom = document.createElement('my-widget');
r.push(custom instanceof HTMLElement);
r.push(!(custom instanceof HTMLUnknownElement));
");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact(Timeout = 600000)]
    public void Character_Data_Document_And_Fragment_Nodes_Answer_Their_Interfaces()
    {
        var result = Run(@"
var t = document.createTextNode('x');
r.push(t instanceof Text);
r.push(t instanceof CharacterData);
r.push(t instanceof Node);
r.push(!(t instanceof Element));

var c = document.createComment('c');
r.push(c instanceof Comment);
r.push(c instanceof CharacterData);
r.push(!(c instanceof Text));

r.push(document instanceof Document);
r.push(document instanceof Node);

var f = document.createDocumentFragment();
r.push(f instanceof DocumentFragment);
r.push(!(f instanceof Element));
");
        Assert.Contains("true,true,true,true,true,true,true,true,true,true,true", result);
    }

    /// <summary>A non-node value is not an instance of any of them, and does not throw.</summary>
    [Fact(Timeout = 600000)]
    public void Non_Node_Values_Are_Not_Instances()
    {
        var result = Run(@"
r.push(!(({}) instanceof HTMLElement));
r.push(!(({ nodeType: 'not a number' }) instanceof Node));
r.push(!([] instanceof Element));
r.push(!(null instanceof HTMLElement));
r.push(!(undefined instanceof Node));
r.push(!('div' instanceof HTMLElement));
");
        Assert.Contains("true,true,true,true,true,true", result);
    }

    /// <summary>
    /// The interfaces are not constructible, exactly as in a browser: their objects come from
    /// <c>document.createElement</c> and friends, so calling one directly throws rather than
    /// handing back a plain object that merely looks like an element.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Interface_Constructors_Are_Not_Directly_Constructible()
    {
        var result = Run(@"
var names = ['Element','HTMLElement','HTMLUnknownElement','Document',
             'DocumentFragment','CharacterData','Text','Comment','Attr'];
for (var i = 0; i < names.length; i++) {
  var threw = false;
  try { new window[names[i]](); } catch (e) { threw = e instanceof TypeError; }
  r.push(threw);
}
");
        Assert.Contains("true,true,true,true,true,true,true,true,true", result);
    }

    /// <summary>
    /// The <c>Node</c> global keeps its DOM type constants — it gained an <c>@@hasInstance</c>,
    /// it was not replaced.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Node_Constants_Survive_Alongside_InstanceOf_Support()
    {
        var result = Run(@"
r.push(Node.ELEMENT_NODE === 1);
r.push(Node.TEXT_NODE === 3);
r.push(Node.COMMENT_NODE === 8);
r.push(Node.DOCUMENT_NODE === 9);
r.push(Node.DOCUMENT_FRAGMENT_NODE === 11);
r.push(document.createElement('div') instanceof Node);
");
        Assert.Contains("true,true,true,true,true,true", result);
    }
}
