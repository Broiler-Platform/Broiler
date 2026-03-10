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
}
