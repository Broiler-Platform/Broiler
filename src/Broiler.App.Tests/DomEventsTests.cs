using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class DomEventsTests
{
    private readonly ScriptEngine _engine = new();

    [Fact]
    public void AddEventListener_And_DispatchEvent_FiresListener()
    {
        var html = "<html><body><div id=\"target\">Click me</div></body></html>";
        var result = _engine.Execute(
            [ @"
                var fired = false;
                var el = document.getElementById('target');
                el.addEventListener('click', function(e) { fired = true; });
                var evt = document.createEvent('Event');
                evt.initEvent('click', true, true);
                el.dispatchEvent(evt);
                if (!fired) throw new Error('event not fired');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void EventObject_HasTypeTargetAndEventPhaseProperties()
    {
        var html = "<html><body><div id=\"target\">Test</div></body></html>";
        var result = _engine.Execute(
            [ @"
                var receivedType = '';
                var el = document.getElementById('target');
                el.addEventListener('test', function(e) {
                    receivedType = e.type;
                    if (typeof e.target === 'undefined') throw new Error('no target');
                    if (typeof e.eventPhase === 'undefined') throw new Error('no eventPhase');
                });
                var evt = document.createEvent('Event');
                evt.initEvent('test', true, true);
                el.dispatchEvent(evt);
                if (receivedType !== 'test') throw new Error('wrong type: ' + receivedType);
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void StopPropagation_PreventsParentFromReceivingEvent()
    {
        var html = "<html><body><div id=\"parent\"><span id=\"child\">Hi</span></div></body></html>";
        var result = _engine.Execute(
            [ @"
                var parentFired = false;
                var parent = document.getElementById('parent');
                var child = document.getElementById('child');
                parent.addEventListener('click', function(e) { parentFired = true; });
                child.addEventListener('click', function(e) { e.stopPropagation(); });
                var evt = document.createEvent('Event');
                evt.initEvent('click', true, true);
                child.dispatchEvent(evt);
                if (parentFired) throw new Error('parent should not have fired');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void PreventDefault_SetsDefaultPrevented()
    {
        var html = "<html><body><div id=\"target\">Test</div></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.getElementById('target');
                el.addEventListener('click', function(e) { e.preventDefault(); });
                var evt = document.createEvent('Event');
                evt.initEvent('click', true, true);
                el.dispatchEvent(evt);
                if (!evt.defaultPrevented) throw new Error('defaultPrevented should be true');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void EventBubbling_BubblesFromChildToParent()
    {
        var html = "<html><body><div id=\"parent\"><span id=\"child\">Hi</span></div></body></html>";
        var result = _engine.Execute(
            [ @"
                var parentFired = false;
                var parent = document.getElementById('parent');
                var child = document.getElementById('child');
                parent.addEventListener('click', function(e) { parentFired = true; });
                var evt = document.createEvent('Event');
                evt.initEvent('click', true, true);
                child.dispatchEvent(evt);
                if (!parentFired) throw new Error('parent should have received bubbled event');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void CreateEvent_And_InitEvent_CreateProperEventObjects()
    {
        var html = "<html><body></body></html>";
        var result = _engine.Execute(
            [ @"
                var evt = document.createEvent('Event');
                evt.initEvent('myevent', true, true);
                if (evt.type !== 'myevent') throw new Error('wrong type');
                if (evt.bubbles !== true) throw new Error('should bubble');
                if (evt.cancelable !== true) throw new Error('should be cancelable');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void RemoveEventListener_RemovesListener()
    {
        var html = "<html><body><div id=\"target\">Test</div></body></html>";
        var result = _engine.Execute(
            [ @"
                var count = 0;
                var el = document.getElementById('target');
                var handler = function(e) { count++; };
                el.addEventListener('click', handler);
                el.removeEventListener('click', handler);
                var evt = document.createEvent('Event');
                evt.initEvent('click', true, true);
                el.dispatchEvent(evt);
                if (count !== 0) throw new Error('listener was not removed');
            " ], html);
        Assert.True(result);
    }
}
