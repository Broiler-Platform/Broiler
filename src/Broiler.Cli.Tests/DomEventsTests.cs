namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 3 Acid3 compliance: DOM Events Level 2/3 —
/// addEventListener/removeEventListener with capture, dispatchEvent with
/// proper propagation phases, stopPropagation, stopImmediatePropagation,
/// preventDefault, inline on* handler properties, element.click(),
/// createEvent/initUIEvent, and nested event dispatch.
/// </summary>
public class DomEventsTests
{
    // ──────────────────────── createEvent / initEvent ────────────────────────

    [Fact]
    public void CreateEvent_Returns_Event_With_InitEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("test,true,false", result);
    }

    [Fact]
    public void CreateEvent_Event_Has_IsTrusted_False()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('Event');
var r = [];
r.push(typeof evt.isTrusted);
r.push(evt.isTrusted);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("boolean,false", result);
    }

    [Fact]
    public void CreateEvent_Event_Has_TimeStamp()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('Event');
var r = [];
r.push(typeof evt.timeStamp);
r.push(evt.timeStamp >= 0);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("number,true", result);
    }

    [Fact]
    public void CreateEvent_Event_Has_Legacy_Alias_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('Event');
var r = [];
r.push(typeof evt.srcElement);
r.push(evt.srcElement === null);
r.push(evt.cancelBubble);
r.push(evt.returnValue);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("object,true,false,true", result);
    }

    [Fact]
    public void CreateEvent_UIEvents_Has_InitUIEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('UIEvents');
evt.initUIEvent('test', true, false, null, 6);
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.detail);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("test,true,false,6", result);
    }

    [Fact]
    public void CreateEvent_MouseEvents_Has_InitMouseEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('MouseEvents');
evt.initMouseEvent('click', true, true, window, 4, 10, 20, 30, 40, true, false, true, false, 2, null);
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.view === window);
r.push(evt.detail);
r.push(evt.screenX);
r.push(evt.screenY);
r.push(evt.clientX);
r.push(evt.clientY);
r.push(evt.ctrlKey);
r.push(evt.altKey);
r.push(evt.shiftKey);
r.push(evt.metaKey);
r.push(evt.button);
r.push(evt.relatedTarget === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("click,true,true,true,4,10,20,30,40,true,false,true,false,2,true", result);
    }

    [Fact]
    public void CreateEvent_MouseEvents_Has_Alias_Properties_And_Default_Button_State()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('MouseEvents');
var before = [evt.x, evt.y, evt.buttons, evt.relatedTarget === null].join(',');
evt.initMouseEvent('click', true, true, window, 1, 10, 20, 30, 40, false, true, false, true, 2, null);
var after = [];
after.push(evt.x);
after.push(evt.y);
after.push(evt.x === evt.clientX);
after.push(evt.y === evt.clientY);
after.push(evt.buttons);
after.push(evt.button);
after.push(evt.relatedTarget === null);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,0,0,true|30,40,true,true,2,2,true", result);
    }

    [Fact]
    public void CreateEvent_FocusEvents_Has_InitFocusEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""related"" />
<div id=""result""></div>
<script>
var related = document.getElementById('related');
var evt = document.createEvent('FocusEvents');
var before = [evt.detail, evt.relatedTarget === null].join(',');
evt.initFocusEvent('focusin', true, false, window, 7, related);
var after = [];
after.push(evt.type);
after.push(evt.bubbles);
after.push(evt.cancelable);
after.push(evt.view === window);
after.push(evt.detail);
after.push(evt.relatedTarget === related);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,true|focusin,true,false,true,7,true", result);
    }

    [Fact]
    public void CreateEvent_KeyboardEvents_Has_InitKeyboardEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('KeyboardEvents');
var before = [evt.key, evt.location, evt.keyCode, evt.charCode, evt.which].join(',');
evt.initKeyboardEvent('keydown', true, false, window, 'Enter', 1, true, false, true, false, false, 13, 0);
var after = [];
after.push(evt.type);
after.push(evt.bubbles);
after.push(evt.cancelable);
after.push(evt.view === window);
after.push(evt.key);
after.push(evt.location);
after.push(evt.ctrlKey);
after.push(evt.altKey);
after.push(evt.shiftKey);
after.push(evt.metaKey);
after.push(evt.keyCode);
after.push(evt.charCode);
after.push(evt.which);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(",0,0,0,0|keydown,true,false,true,Enter,1,true,false,true,false,13,0,13", result);
    }

    [Fact]
    public void CreateEvent_KeyboardEvents_Has_Repeat_Property()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('KeyboardEvents');
var before = [typeof evt.repeat, evt.repeat].join(',');
evt.initKeyboardEvent('keydown', true, false, window, 'Enter', 1, true, false, true, false, true, 13, 0);
var after = [];
after.push(evt.repeat);
after.push(evt.keyCode);
after.push(evt.charCode);
after.push(evt.which);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("boolean,false|true,13,0,13", result);
    }

    [Fact]
    public void CreateEvent_WheelEvents_Has_InitWheelEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('WheelEvents');
var before = [evt.deltaX, evt.deltaY, evt.deltaZ, evt.deltaMode].join(',');
evt.initWheelEvent('wheel', true, true, window, 4, 10, 20, 30, 40, 0, null, 'Control Shift', 1.5, -2.5, 0, 1);
var after = [];
after.push(evt.type);
after.push(evt.bubbles);
after.push(evt.cancelable);
after.push(evt.view === window);
after.push(evt.detail);
after.push(evt.clientX);
after.push(evt.clientY);
after.push(evt.x);
after.push(evt.y);
after.push(evt.ctrlKey);
after.push(evt.altKey);
after.push(evt.shiftKey);
after.push(evt.metaKey);
after.push(evt.deltaX);
after.push(evt.deltaY);
after.push(evt.deltaZ);
after.push(evt.deltaMode);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,0,0,0|wheel,true,true,true,4,30,40,30,40,true,false,true,false,1.5,-2.5,0,1", result);
    }

    [Fact]
    public void CreateEvent_CustomEvent_Has_InitCustomEvent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = document.createEvent('CustomEvent');
var before = [typeof evt.initCustomEvent, evt.detail].join(',');
evt.initCustomEvent('build', true, false, 'payload');
var after = [];
after.push(evt.type);
after.push(evt.bubbles);
after.push(evt.cancelable);
after.push(evt.detail);
document.getElementById('result').textContent = before + '|' + after.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("function,0|build,true,false,payload", result);
    }

    [Fact]
    public void Event_Constructor_Seeds_Init_Dictionary()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = new Event('test', { bubbles: true, cancelable: false });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(typeof evt.isTrusted);
r.push(evt.isTrusted);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("test,true,false,boolean,false", result);
    }

    [Fact]
    public void CustomEvent_Constructor_Reuses_CreateEvent_Surface()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = new CustomEvent('build', { bubbles: true, cancelable: false, detail: 'payload' });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.detail);
r.push(typeof evt.timeStamp);
r.push(evt.srcElement === null);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("build,true,false,payload,number,true", result);
    }

    [Fact]
    public void MouseEvent_Constructor_Seeds_Options()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = new MouseEvent('click', { bubbles: true, cancelable: true, detail: 4, clientX: 30, clientY: 40, ctrlKey: true, button: 2 });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.detail);
r.push(evt.clientX);
r.push(evt.clientY);
r.push(evt.x);
r.push(evt.y);
r.push(evt.ctrlKey);
r.push(evt.button);
r.push(evt.buttons);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("click,true,true,4,30,40,30,40,true,2,2", result);
    }

    [Fact]
    public void FocusEvent_Constructor_Seeds_Options()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""related"" />
<div id=""result""></div>
<script>
var related = document.getElementById('related');
var evt = new FocusEvent('focusin', { bubbles: true, cancelable: false, detail: 7, relatedTarget: related });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.detail);
r.push(evt.relatedTarget === related);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("focusin,true,false,7,true", result);
    }

    [Fact]
    public void KeyboardEvent_Constructor_Seeds_Options()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = new KeyboardEvent('keydown', { bubbles: true, cancelable: false, key: 'Enter', location: 1, ctrlKey: true, shiftKey: true, repeat: true, keyCode: 13 });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.key);
r.push(evt.location);
r.push(evt.ctrlKey);
r.push(evt.shiftKey);
r.push(evt.repeat);
r.push(evt.keyCode);
r.push(evt.which);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("keydown,true,false,Enter,1,true,true,true,13,13", result);
    }

    [Fact]
    public void WheelEvent_Constructor_Seeds_Options()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = new WheelEvent('wheel', { bubbles: true, cancelable: true, detail: 4, clientX: 30, clientY: 40, ctrlKey: true, shiftKey: true, deltaX: 1.5, deltaY: -2.5, deltaMode: 1 });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.detail);
r.push(evt.clientX);
r.push(evt.clientY);
r.push(evt.ctrlKey);
r.push(evt.shiftKey);
r.push(evt.deltaX);
r.push(evt.deltaY);
r.push(evt.deltaMode);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("wheel,true,true,4,30,40,true,true,1.5,-2.5,1", result);
    }

    [Fact]
    public void UIEvent_Constructor_Seeds_Options()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var evt = new UIEvent('build', { bubbles: true, cancelable: false, view: window, detail: 6 });
var r = [];
r.push(evt.type);
r.push(evt.bubbles);
r.push(evt.cancelable);
r.push(evt.view === window);
r.push(evt.detail);
r.push(typeof evt.timeStamp);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("build,true,false,true,6,number", result);
    }

    // ──────────────────────── addEventListener / removeEventListener ────────────────────────

    [Fact]
    public void AddEventListener_And_DispatchEvent_Basic()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
target.addEventListener('test', function(e) { count++; }, false);
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
target.dispatchEvent(evt);
target.dispatchEvent(evt);
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">2<", result);
    }

    [Fact]
    public void RemoveEventListener_Prevents_Further_Calls()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
var handler = function(e) { count++; };
target.addEventListener('test', handler, false);
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
target.dispatchEvent(evt);
target.removeEventListener('test', handler, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">1<", result);
    }

    // ──────────────────────── Event bubbling ────────────────────────

    [Fact]
    public void Event_Bubbles_To_Parent()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var phases = [];
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('test', function(e) {
    phases.push('parent-bubble-phase' + e.eventPhase);
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
child.dispatchEvent(evt);
document.getElementById('result').textContent = phases.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Parent receives during bubble phase (phase 3)
        Assert.Contains("parent-bubble-phase3", result);
    }

    [Fact]
    public void NonBubbling_Event_Does_Not_Bubble()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var count = 0;
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('test', function(e) { count++; }, false);
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
child.dispatchEvent(evt);
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Bubble listener on parent should NOT fire for non-bubbling event
        Assert.Contains(">0<", result);
    }

    // ──────────────────────── Capture phase ────────────────────────

    [Fact]
    public void Capture_Listener_Fires_In_Capture_Phase()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var phases = [];
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('test', function(e) {
    phases.push('capture-phase' + e.eventPhase);
}, true);
parent.addEventListener('test', function(e) {
    phases.push('bubble-phase' + e.eventPhase);
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
child.dispatchEvent(evt);
document.getElementById('result').textContent = phases.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Capture fires during phase 1, bubble during phase 3
        Assert.Contains("capture-phase1,bubble-phase3", result);
    }

    [Fact]
    public void Capture_Listener_Not_Fired_During_Bubble()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var captureCount = 0;
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('test', function(e) { captureCount++; }, true);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
child.dispatchEvent(evt);
document.getElementById('result').textContent = captureCount;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Capture listener fires once during capture phase only
        Assert.Contains(">1<", result);
    }

    // ──────────────────────── stopPropagation ────────────────────────

    [Fact]
    public void StopPropagation_Prevents_Bubble_But_Fires_All_Current_Node_Listeners()
    {
        // This mirrors Acid3 test 31: stopPropagation on a capture handler
        // should NOT stop other capture handlers on the SAME node,
        // but should stop the event from reaching other nodes.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var captureCount = 0;
var bubbleCount = 0;
var div = document.createElement('div');
var input = document.createElement('input');
div.appendChild(input);
document.body.appendChild(div);

div.addEventListener('click', function(e) {
    captureCount++;
    e.stopPropagation();
}, true);
div.addEventListener('click', function(e) {
    captureCount++;
}, true);
div.addEventListener('click', function(e) {
    bubbleCount++;
}, false);

input.click();
document.body.removeChild(div);
document.getElementById('result').textContent = captureCount + ',' + bubbleCount;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Both capture handlers fire (2), bubble handler doesn't (0)
        Assert.Contains("2,0", result);
    }

    [Fact]
    public void StopImmediatePropagation_Stops_Remaining_Listeners_On_Same_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
target.addEventListener('test', function(e) {
    count++;
    e.stopImmediatePropagation();
}, false);
target.addEventListener('test', function(e) {
    count++;
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Only the first listener fires
        Assert.Contains(">1<", result);
    }

    // ──────────────────────── preventDefault ────────────────────────

    [Fact]
    public void PreventDefault_Sets_DefaultPrevented_And_Returns_False()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var target = document.getElementById('target');
target.addEventListener('test', function(e) {
    e.preventDefault();
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', false, true);
var returned = target.dispatchEvent(evt);
document.getElementById('result').textContent = returned + ',' + evt.defaultPrevented;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("false,true", result);
    }

    [Fact]
    public void Legacy_Event_Aliases_Track_Dispatch_State()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var parentHits = 0;
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('test', function() {
    parentHits++;
}, false);
child.addEventListener('test', function(e) {
    var r = [];
    r.push(e.srcElement === child);
    e.cancelBubble = true;
    e.returnValue = false;
    r.push(e.cancelBubble);
    r.push(e.returnValue);
    window.__legacyResult = r.join(',');
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, true);
var returned = child.dispatchEvent(evt);
document.getElementById('result').textContent = [
    window.__legacyResult,
    parentHits,
    returned,
    evt.defaultPrevented,
    evt.cancelBubble,
    evt.returnValue
].join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,false|0|false|true|true|false", result);
    }

    // ──────────────────────── element.click() ────────────────────────

    [Fact]
    public void Element_Click_Dispatches_Click_Event()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
target.addEventListener('click', function(e) {
    count++;
}, false);
target.click();
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">1<", result);
    }

    [Fact]
    public void Element_Click_Bubbles_And_Has_Correct_Properties()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var r = [];
var parent = document.getElementById('parent');
var child = document.getElementById('child');
parent.addEventListener('click', function(e) {
    r.push(e.type);
    r.push(e.bubbles);
    r.push(e.cancelable);
    r.push(e.eventPhase);
}, false);
child.click();
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("click,true,true,3", result);
    }

    [Fact]
    public void Element_Focus_Dispatches_Focus_Event()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""target"">
<div id=""result""></div>
<script>
var r = [];
var target = document.getElementById('target');
target.addEventListener('focus', function(e) {
    r.push(e.type);
    r.push(e.bubbles);
    r.push(e.cancelable);
    r.push(e.eventPhase);
    r.push(typeof e.timeStamp);
}, false);
target.focus();
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("focus,false,false,2,number", result);
    }

    [Fact]
    public void Element_Blur_Dispatches_Blur_Event()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input id=""target"">
<div id=""result""></div>
<script>
var r = [];
var target = document.getElementById('target');
target.addEventListener('blur', function(e) {
    r.push(e.type);
    r.push(e.bubbles);
    r.push(e.cancelable);
    r.push(e.eventPhase);
    r.push(typeof e.timeStamp);
}, false);
target.blur();
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("blur,false,false,2,number", result);
    }

    // ──────────────────────── on* inline handler properties ────────────────────────

    [Fact]
    public void Onclick_Property_Fires_On_Click()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
target.onclick = function(e) { count++; };
target.click();
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">1<", result);
    }

    [Fact]
    public void Onclick_Null_Removes_Handler()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var count = 0;
var target = document.getElementById('target');
target.onclick = function(e) { count++; };
target.click();
target.onclick = null;
target.click();
document.getElementById('result').textContent = count;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">1<", result);
    }

    [Fact]
    public void Onclick_Property_Getter_Returns_Handler()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var target = document.getElementById('target');
var before = target.onclick;
var fn = function(e) {};
target.onclick = fn;
var after = target.onclick;
document.getElementById('result').textContent = (before === null) + ',' + (after === fn);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true", result);
    }

    // ──────────────────── Acid3-style event dispatch tests ────────────────────

    [Fact]
    public void Acid3_Test30_DispatchEvent_Bubbles_And_RemoveListener()
    {
        // Mirrors Acid3 test 30: addEventListener + createEvent + initUIEvent +
        // dispatchEvent (bubbles from child to parent) + removeEventListener
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""><span id=""score"">0</span> text</div>
<div id=""out""></div>
<script>
var count = 0;
var ok = true;
var test = function(event) {
    if (event.detail != 6) ok = false;
    count++;
};
document.getElementById('result').addEventListener('test', test, false);
var event = document.createEvent('UIEvents');
event.initUIEvent('test', true, false, null, 6);
var r1 = document.getElementById('score').dispatchEvent(event);
var scoreNext = document.getElementById('score').nextSibling;
var r2 = scoreNext.dispatchEvent(event);
document.getElementById('result').removeEventListener('test', test, false);
var r3 = document.getElementById('score').dispatchEvent(event);
document.getElementById('out').textContent = count + ',' + ok + ',' + r1 + ',' + r2 + ',' + r3;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // count=2 (two dispatches before removal), ok=true (detail===6), all return true
        Assert.Contains("2,true,true,true,true", result);
    }

    [Fact]
    public void Acid3_Test31_StopPropagation_And_Capture()
    {
        // Mirrors Acid3 test 31: capture phase listeners, stopPropagation,
        // element.click(), event properties
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var input = document.createElement('input');
var div = document.createElement('div');
div.appendChild(input);
document.body.appendChild(div);

var ok = true;
var captureCount = 0;
var testCapture = function(event) {
    ok = ok &&
         (event.type == 'click') &&
         (event.target == input) &&
         (event.currentTarget == div) &&
         (event.eventPhase == 1) &&
         (event.bubbles) &&
         (event.cancelable);
    captureCount++;
    event.stopPropagation();
};
var testBubble = function(event) {
    ok = false;
};
div.addEventListener('click', function(event) { testCapture(event) }, true);
div.addEventListener('click', function(event) { testCapture(event) }, true);
div.addEventListener('click', testBubble, false);
input.type = 'reset';
input.click();
document.body.removeChild(div);
document.getElementById('result').textContent = captureCount + ',' + ok;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // captureCount=2, ok=true
        Assert.Contains("2,true", result);
    }

    [Fact]
    public void Acid3_Test32_Bubbling_Through_Body()
    {
        // Mirrors Acid3 test 32: event bubbling through body with eventPhase=3
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var ok = true;
var count = 0;
var test = function(event) {
    count += 1;
    if (event.eventPhase != 3) ok = false;
};
document.body.addEventListener('click', test, false);
var input = document.createElement('input');
var div = document.createElement('div');
div.appendChild(input);
document.body.appendChild(div);
input.type = 'reset';
input.click();
document.body.removeEventListener('click', test, false);
input.click();
document.body.removeChild(div);
document.getElementById('result').textContent = count + ',' + ok;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // count=1 (only first click reaches body listener), ok=true (phase=3)
        Assert.Contains("1,true", result);
    }

    [Fact]
    public void Nested_Event_Dispatch_Recursive()
    {
        // Mirrors Acid3 test 73: nested/recursive event dispatch
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var up = 0;
var down = 0;
var button = document.createElement('button');
button.type = 'button';
button.onclick = function() { up += 1; if (up < 10) button.click(); down += up; };
button.addEventListener('test', function() {
    up += 1;
    var e = document.createEvent('HTMLEvents');
    e.initEvent('test', false, false);
    if (up < 20) button.dispatchEvent(e);
    down += up;
}, false);
var evt = document.createEvent('HTMLEvents');
evt.initEvent('test', false, false);
button.dispatchEvent(evt);
document.getElementById('result').textContent = up + ',' + down;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // up=20, down=400 (sum of 20+19+18+...+1 = 210? No - each recursive call adds current up)
        // Actually: up goes 1,2,3,...,20. down accumulates: 20+20+20+...+20 (20 times) = 400
        Assert.Contains("20,400", result);
    }

    // ──────────────────── Target phase fires all listeners ────────────────────

    [Fact]
    public void Target_Phase_Fires_Both_Capture_And_Bubble_Listeners()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var order = [];
var target = document.getElementById('target');
target.addEventListener('test', function(e) {
    order.push('capture-' + e.eventPhase);
}, true);
target.addEventListener('test', function(e) {
    order.push('bubble-' + e.eventPhase);
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
target.dispatchEvent(evt);
document.getElementById('result').textContent = order.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // At target phase (phase 2), all listeners fire in registration order
        Assert.Contains("capture-2,bubble-2", result);
    }

    // ──────────────────── Event target property ────────────────────

    [Fact]
    public void Event_Target_Is_The_Dispatch_Element()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><div id=""child""></div></div>
<div id=""result""></div>
<script>
var parent = document.getElementById('parent');
var child = document.getElementById('child');
var targetId = '';
parent.addEventListener('test', function(e) {
    targetId = e.target.id;
}, false);
var evt = document.createEvent('Event');
evt.initEvent('test', true, false);
child.dispatchEvent(evt);
document.getElementById('result').textContent = targetId;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">child<", result);
    }

    // ──────────────────── dispatchEvent return value ────────────────────

    [Fact]
    public void DispatchEvent_Returns_True_When_Not_Prevented()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""></div>
<div id=""result""></div>
<script>
var target = document.getElementById('target');
var evt = document.createEvent('Event');
evt.initEvent('test', false, false);
var r = target.dispatchEvent(evt);
document.getElementById('result').textContent = '' + r;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">true<", result);
    }

    // ──────────────────── Event handler attribute (getAttribute) ────────────────────

    [Fact]
    public void GetAttribute_Returns_Event_Handler_String()
    {
        // Mirrors Acid3 test 24: getAttribute('onload') returns the attribute string
        var html = @"<!DOCTYPE html>
<html><body onload=""doStuff()"">
<div id=""result""></div>
<script>
var val = document.body.getAttribute('onload');
document.getElementById('result').textContent = val;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("doStuff()", result);
    }
}
