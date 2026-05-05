namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 5 (v2) Acid3 compliance: DOM Events Edge Cases —
/// event handler attribute reflection, document-level event bubbling,
/// event dispatch on text nodes, detached DOM node re-attachment,
/// and nested (re-entrant) event dispatch.
/// </summary>
public class DomEventsEdgeCaseTests
{
    // ──────────────────── 5.1 Event handler attribute reflection ────────────────────

    [Fact]
    public void OnClick_Getter_Returns_Function_From_HTML_Attribute()
    {
        // element.onclick must return a function (not a string) when set via HTML attribute
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""btn"" onclick=""doStuff()""></div>
<div id=""result""></div>
<script>
var btn = document.getElementById('btn');
var r = typeof btn.onclick;
document.getElementById('result').textContent = r;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("function", result);
    }

    [Fact]
    public void OnClick_Null_Removes_Handler()
    {
        // Setting element.onclick = null must remove the handler
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""btn"" onclick=""doStuff()""></div>
<div id=""result""></div>
<script>
var btn = document.getElementById('btn');
btn.onclick = null;
var r = [];
r.push('' + btn.onclick);
r.push(btn.getAttribute('onclick'));
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // onclick property returns null; getAttribute still returns source string
        Assert.Contains("null|doStuff()", result);
    }

    [Fact]
    public void GetAttribute_Returns_Source_String_SetAttribute_Compiles()
    {
        // setAttribute('onclick', code) should compile the code AND store the string
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""btn""></div>
<div id=""result""></div>
<script>
var btn = document.getElementById('btn');
btn.setAttribute('onclick', 'window.clicked = true');
var r = [];
r.push(btn.getAttribute('onclick'));
r.push(typeof btn.onclick);
window.clicked = false;
btn.click();
r.push('' + window.clicked);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // getAttribute returns source string, onclick returns function, click fires the handler
        Assert.Contains("window.clicked = true,function,true", result);
    }

    // ──────────────────── 5.2 Document-level event bubbling ────────────────────

    [Fact]
    public void Document_AddEventListener_Receives_Bubbled_Events()
    {
        // Events must bubble from element → body → html → document
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var r = [];
document.addEventListener('test', function(e) {
    r.push('doc');
}, false);
document.documentElement.addEventListener('test', function(e) {
    r.push('html');
}, false);
document.body.addEventListener('test', function(e) {
    r.push('body');
}, false);
var target = document.getElementById('target');
target.addEventListener('test', function(e) {
    r.push('target');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Bubble order: target → body → html → document
        Assert.Contains("target,body,html,doc", result);
    }

    [Fact]
    public void Document_Capture_Phase_Fires_Before_Target()
    {
        // document.addEventListener with capture=true should fire first (capture phase)
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var r = [];
document.addEventListener('test', function(e) {
    r.push('doc-capture');
}, true);
document.addEventListener('test', function(e) {
    r.push('doc-bubble');
}, false);
var target = document.getElementById('target');
target.addEventListener('test', function(e) {
    r.push('target');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Capture: doc-capture → target → bubble: doc-bubble
        Assert.Contains("doc-capture,target,doc-bubble", result);
    }

    [Fact]
    public void AddEventListener_OptionsObject_Capture_And_Remove_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var r = [];
var parent = document.getElementById('parent');
var child = document.getElementById('child');
var handler = function(e) { r.push('capture'); };
parent.addEventListener('test', handler, { capture: true });
parent.removeEventListener('test', handler, { capture: true });
parent.addEventListener('test', function(e) { r.push('bubble'); }, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
child.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">bubble<", result);
        Assert.DoesNotContain(">capture<", result);
    }

    [Fact]
    public void AddEventListener_Once_Option_Fires_Only_Once()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
target.addEventListener('test', function(e) { count++; }, { once: true });
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
target.dispatchEvent(evt);
target.dispatchEvent(evt);
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">1<", result);
    }

    [Fact]
    public void Passive_Listener_PreventDefault_Does_Not_Cancel_Event()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var target = document.getElementById('target');
var evt = document.createEvent('Event');
evt.initEvent('test', true, true);
target.addEventListener('test', function(e) { e.preventDefault(); }, { passive: true });
var r = [];
r.push('' + target.dispatchEvent(evt));
r.push('' + evt.defaultPrevented);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,false", result);
    }

    [Fact]
    public void Focus_And_Blur_Do_Not_Bubble()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><input id=""child""></div>
<div id=""result""></div>
<script>
var r = [];
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('focus', function(e) { r.push('parent-focus'); }, false);
parent.addEventListener('blur', function(e) { r.push('parent-blur'); }, false);
child.addEventListener('focus', function(e) { r.push('child-focus'); }, false);
child.addEventListener('blur', function(e) { r.push('child-blur'); }, false);
child.focus();
child.blur();
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">child-focus,child-blur<", result);
    }

    [Fact]
    public void Event_ComposedPath_Includes_Target_Ancestors_Document_And_Window()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('test', function(e) {
    var path = e.composedPath();
    var names = [];
    for (var i = 0; i < path.length; i++) {
        var node = path[i];
        if (node === window) names.push('window');
        else names.push((node.id || node.nodeName || '').toLowerCase());
    }
    document.getElementById('result').textContent = path.length + '|' + names.slice(0, 5).join('>');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
child.dispatchEvent(evt);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("6|child&gt;parent&gt;body&gt;html&gt;#document", result);
    }

    [Fact]
    public void Window_AddEventListener_Once_Option_Fires_Only_Once()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var count = 0;
window.addEventListener('test', function(e) { count++; }, { once: true });
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
window.dispatchEvent(evt);
window.dispatchEvent(evt);
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">1<", result);
    }

    // ──────────────────── 5.3 Event dispatch on text nodes ────────────────────

    [Fact]
    public void TextNode_DispatchEvent_Bubbles_To_Document()
    {
        // textNode.dispatchEvent must work and events must bubble to document
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container"">hello</div>
<div id=""result""></div>
<script>
var r = [];
var container = document.getElementById('container');
var textNode = container.firstChild;
container.addEventListener('test', function(e) {
    r.push('container');
}, false);
document.addEventListener('test', function(e) {
    r.push('doc');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
textNode.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Bubble: text → container → body → html → doc
        Assert.Contains("container,doc", result);
    }

    // ──────────────────── 5.4 DOM tree GC survival ────────────────────

    [Fact]
    public void Detached_Node_Reattach_Preserves_Identity()
    {
        // JS-held references to removed nodes must keep them alive and re-attachable
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><span id=""child"">hello</span></div>
<div id=""result""></div>
<script>
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.removeChild(child);
var r = [];
r.push(child.id);
r.push(child.textContent);
r.push('' + (child.parentNode === null));
parent.appendChild(child);
r.push('' + (child.parentNode === parent));
r.push(parent.children.length);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("child,hello,true,true,1", result);
    }

    // ──────────────────── 5.5 Nested event dispatch ────────────────────

    [Fact]
    public void Nested_DispatchEvent_Inside_Handler_Works()
    {
        // Dispatching an event inside an event handler must work correctly
        // and must not corrupt the outer event's state
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var r = [];
var target = document.getElementById('target');
target.addEventListener('outer', function(e) {
    r.push('outer-start');
    var inner = document.createEvent('Event');
    inner.initEvent('inner', true, false);
    target.dispatchEvent(inner);
    r.push('outer-end');
}, false);
target.addEventListener('inner', function(e) {
    r.push('inner');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('outer', true, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Nested dispatch: outer handler starts, inner fires completely, outer continues
        Assert.Contains("outer-start,inner,outer-end", result);
    }

    // ──────────────────── Acid3 Bucket 2 regression tests (17–32 gaps) ────────────────────

    [Fact]
    public void Acid3_Test20_Null_Bytes_In_Element_Names_Attributes_And_Text()
    {
        // Null bytes (\0) in attribute values and text content must not crash the DOM
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
try {
    var div = document.createElement('div');
    div.setAttribute('data-x', 'a\0b');
    r.push('attr-ok');
    r.push(div.getAttribute('data-x').length >= 3 ? 'len-ok' : 'len-bad');
    div.textContent = 'hello\0world';
    r.push('text-ok');
    r.push(div.textContent.length >= 11 ? 'tlen-ok' : 'tlen-bad');
    document.body.appendChild(div);
    r.push(div.parentNode === document.body ? 'attached' : 'detached');
    document.body.removeChild(div);
    r.push('dom-ok');
} catch(e) {
    r.push('error:' + e.message);
}
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // DOM remains functional after null-byte operations
        Assert.Contains("attr-ok", result);
        Assert.Contains("text-ok", result);
        Assert.Contains("dom-ok", result);
    }

    [Fact]
    public void Acid3_Test24_SetAttribute_OnClick_Compiles_And_Fires()
    {
        // setAttribute('onclick', code) must compile the code as a function and fire on dispatch
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""btn""></div>
<div id=""result""></div>
<script>
var r = [];
var btn = document.getElementById('btn');
window.handlerResult = false;
btn.setAttribute('onclick', 'window.handlerResult = true');
r.push(typeof btn.onclick);
var evt = document.createEvent('Event');
evt.initEvent('click', true, true);
btn.dispatchEvent(evt);
r.push('fired:' + window.handlerResult);
document.getElementById('result').textContent = r.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // setAttribute compiles the string to a function and dispatching click fires it
        Assert.Contains("function", result);
        Assert.Contains("fired:true", result);
    }

    [Fact]
    public void Acid3_Test26_Document_Tree_Lifecycle_Cross_Document_Move()
    {
        // Moving nodes between documents: create in one doc, append in another
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var newDoc = document.implementation.createDocument(null, 'root', null);
        r.push('D' + (newDoc.documentElement.tagName.toLowerCase() === 'root' ? '1' : '0'));
var div = document.createElement('div');
div.setAttribute('id', 'moved');
div.textContent = 'content';
r.push('O' + (div.ownerDocument === document ? '1' : '0'));
        // Cross-document appendChild (implicitly adopts)
        newDoc.documentElement.appendChild(div);
        r.push('P' + (div.parentNode === newDoc.documentElement ? '1' : '0'));
        r.push('N' + (div.ownerDocument === newDoc ? '1' : '0'));
        r.push('T' + div.textContent);
        r.push('I' + div.getAttribute('id'));
        document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Node moved cross-document preserves attributes and content
        Assert.Contains("D1,O1,P1,N1,Tcontent,Imoved", result);
    }

    [Fact]
    public void Acid3_Test27_Cross_Document_Subtree_Preserves_Structure()
    {
        // Moving a subtree cross-document preserves children, attributes, and text
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var newDoc = document.implementation.createDocument(null, 'root', null);
var parent = document.createElement('div');
parent.setAttribute('class', 'wrapper');
var child = document.createElement('span');
child.setAttribute('data-val', '42');
child.textContent = 'kept';
parent.appendChild(child);
// Cross-document appendChild
        newDoc.documentElement.appendChild(parent);
        r.push('A' + parent.getAttribute('class'));
        r.push('C' + parent.childNodes.length);
        r.push('T' + parent.firstChild.tagName.toLowerCase());
        r.push('V' + parent.firstChild.getAttribute('data-val'));
        r.push('X' + parent.firstChild.textContent);
        r.push('N' + (parent.ownerDocument === newDoc ? '1' : '0'));
        r.push('M' + (parent.firstChild.ownerDocument === newDoc ? '1' : '0'));
        r.push('P' + (parent.parentNode === newDoc.documentElement ? '1' : '0'));
        document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Subtree structure, attributes, and text survive cross-document move
        Assert.Contains("Awrapper,C1,Tspan,V42,Xkept,N1,M1,P1", result);
    }

    [Fact]
    public void Acid3_Test29_CloneNode_Deep_Preserves_Whitespace_Text_Nodes()
    {
        // cloneNode(true) must preserve whitespace text nodes between elements
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var r = [];
var container = document.createElement('div');
var s1 = document.createElement('span');
s1.textContent = 'a';
var ws = document.createTextNode(' ');
var s2 = document.createElement('span');
s2.textContent = 'b';
container.appendChild(s1);
container.appendChild(ws);
container.appendChild(s2);
var clone = container.cloneNode(true);
r.push(clone.childNodes.length);
var hasWhitespace = false;
for (var i = 0; i < clone.childNodes.length; i++) {
    var n = clone.childNodes[i];
    if (n.nodeType === 3 && /^\s+$/.test(n.nodeValue)) {
        hasWhitespace = true;
    }
}
r.push(hasWhitespace ? 'ws-yes' : 'ws-no');
r.push(clone.firstChild.tagName ? clone.firstChild.tagName.toLowerCase() : 'text');
r.push(clone.lastChild.tagName ? clone.lastChild.tagName.toLowerCase() : 'text');
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Three child nodes: <span>, whitespace text, <span>
        Assert.Contains("3,ws-yes,span,span", result);
    }

    [Fact]
    public void Acid3_Test31_StopPropagation_During_Capture_Prevents_Target_And_Bubble()
    {
        // stopPropagation() in capture phase must prevent target and bubble phases
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""outer""><div id=""inner""></div></div>
<div id=""result""></div>
<script>
var r = [];
var outer = document.getElementById('outer');
var inner = document.getElementById('inner');
outer.addEventListener('test', function(e) {
    r.push('C1');
    e.stopPropagation();
}, true);
inner.addEventListener('test', function(e) {
    r.push('T1');
}, false);
outer.addEventListener('test', function(e) {
    r.push('B1');
}, false);
document.addEventListener('test', function(e) {
    r.push('D1');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
inner.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Only capture listener fires; target and bubble are suppressed
        Assert.Contains(">C1<", result);
    }

    [Fact]
    public void Acid3_Test32_Event_Bubbles_Full_Chain_Target_Parent_Body_Html_Document()
    {
        // Events must bubble through the full chain with order tracking
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""target""></div></div>
<div id=""result""></div>
<script>
var r = [];
var target = document.getElementById('target');
var parent = document.getElementById('parent');
target.addEventListener('test', function(e) {
    r.push('target:' + e.eventPhase);
}, false);
parent.addEventListener('test', function(e) {
    r.push('parent:' + e.eventPhase);
}, false);
document.body.addEventListener('test', function(e) {
    r.push('body:' + e.eventPhase);
}, false);
document.documentElement.addEventListener('test', function(e) {
    r.push('html:' + e.eventPhase);
}, false);
document.addEventListener('test', function(e) {
    r.push('doc:' + e.eventPhase);
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Full bubble chain: target(2) → parent(3) → body(3) → html(3) → doc(3)
        Assert.Contains("target:2,parent:3,body:3,html:3,doc:3", result);
    }

    [Fact]
    public void Multiple_Capture_Listeners_Same_Element_Fire_In_Registration_Order()
    {
        // Multiple addEventListener calls with capture=true on the same element fire in order
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""outer""><div id=""inner""></div></div>
<div id=""result""></div>
<script>
var r = [];
var outer = document.getElementById('outer');
var inner = document.getElementById('inner');
outer.addEventListener('test', function(e) {
    r.push('cap-1');
}, true);
outer.addEventListener('test', function(e) {
    r.push('cap-2');
}, true);
outer.addEventListener('test', function(e) {
    r.push('cap-3');
}, true);
inner.addEventListener('test', function(e) {
    r.push('target');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
inner.dispatchEvent(evt);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Capture listeners fire in registration order
        Assert.Contains("cap-1,cap-2,cap-3,target", result);
    }
}
