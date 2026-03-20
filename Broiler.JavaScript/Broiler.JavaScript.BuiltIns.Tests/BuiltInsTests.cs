using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Events;
using Broiler.JavaScript.Core.Core.Weak;

namespace Broiler.JavaScript.BuiltIns.Tests;

// ---------------------------------------------------------------
// WeakRef tests
// ---------------------------------------------------------------

public class WeakRefTests : IDisposable
{
    private readonly JSContext _context;

    public WeakRefTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void WeakRef_IsRegisteredAsFunction()
    {
        var result = _context.Eval("typeof WeakRef");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void WeakRef_CanConstructAndDeref()
    {
        var result = _context.Eval(@"
            var obj = { name: 'test' };
            var wr = new WeakRef(obj);
            wr.deref().name;
        ");
        Assert.Equal("test", result.ToString());
    }

    [Fact]
    public void WeakRef_DerefReturnsOriginalObject()
    {
        var result = _context.Eval(@"
            var obj = { x: 42 };
            var wr = new WeakRef(obj);
            wr.deref() === obj;
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void WeakRef_CSharp_DerefReturnsValue()
    {
        var target = new Broiler.JavaScript.Core.JSObject();
        var wr = new JSWeakRef(target);
        var derefed = wr.Deref(new Arguments());
        Assert.False(derefed.IsUndefined);
    }
}

// ---------------------------------------------------------------
// FinalizationRegistry tests
// ---------------------------------------------------------------

public class FinalizationRegistryTests : IDisposable
{
    private readonly JSContext _context;

    public FinalizationRegistryTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void FinalizationRegistry_IsRegisteredAsFunction()
    {
        var result = _context.Eval("typeof FinalizationRegistry");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void FinalizationRegistry_RequiresFunctionArgument()
    {
        Assert.Throws<JSException>(() =>
        {
            _context.Eval("new FinalizationRegistry(42)");
        });
    }

    [Fact]
    public void FinalizationRegistry_CanConstructWithCallback()
    {
        // Should not throw — valid construction with a callback
        var result = _context.Eval(@"
            var fr = new FinalizationRegistry(function(value) { });
            typeof fr;
        ");
        Assert.Equal("object", result.ToString());
    }
}

// ---------------------------------------------------------------
// EventTarget tests
// ---------------------------------------------------------------

public class EventTargetTests : IDisposable
{
    private readonly JSContext _context;

    public EventTargetTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void EventTarget_IsRegisteredAsFunction()
    {
        var result = _context.Eval("typeof EventTarget");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void EventTarget_CanConstruct()
    {
        var result = _context.Eval(@"
            var et = new EventTarget();
            typeof et;
        ");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void EventTarget_CSharp_CanDispatchEvent()
    {
        // Test EventTarget.DispatchEvent via C# API
        var et = new EventTarget(new Arguments());
        var e = Event.Create("test");
        var result = et.DispatchEvent(e);
        // DispatchEvent returns a value (undefined if no handlers)
        Assert.NotNull(result);
    }

    [Fact]
    public void EventTarget_CSharp_EventHasType()
    {
        var e = Event.Create("myevent");
        Assert.Equal("myevent", e.Type);
    }
}

// ---------------------------------------------------------------
// Event tests (C# API - Event is not JS-registered via JSClassGenerator)
// ---------------------------------------------------------------

public class EventTests : IDisposable
{
    private readonly JSContext _context;

    public EventTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Event_CSharp_CanCreateWithType()
    {
        var e = Event.Create("click");
        Assert.Equal("click", e.Type);
    }

    [Fact]
    public void Event_CSharp_BubblesDefaultFalse()
    {
        var e = Event.Create("click");
        Assert.False(e.Bubbles.BooleanValue);
    }

    [Fact]
    public void Event_CSharp_CreateSetsType()
    {
        // Event.Create factory produces a correctly typed event
        var e = Event.Create("keydown");
        Assert.Equal("keydown", e.Type);
        Assert.False(e.Bubbles.BooleanValue);
        Assert.False(e.Cancelable.BooleanValue);
    }
}

// ---------------------------------------------------------------
// DefaultBuiltInRegistry.AdditionalRegistrations tests
// ---------------------------------------------------------------

public class AdditionalRegistrationsTests : IDisposable
{
    private readonly IBuiltInRegistry _originalRegistry;

    public AdditionalRegistrationsTests()
    {
        _originalRegistry = JSContext.BuiltInRegistry;
    }

    public void Dispose()
    {
        JSContext.BuiltInRegistry = _originalRegistry;
    }

    [Fact]
    public void AdditionalRegistrations_IsSet()
    {
        // The BuiltIns assembly module initializer should have set this
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
    }

    [Fact]
    public void AdditionalRegistrations_InvokedDuringContextCreation()
    {
        // WeakRef and EventTarget should be available via AdditionalRegistrations
        using var ctx = new JSContext();
        JSContext.CurrentContext = ctx;

        Assert.Equal("function", ctx.Eval("typeof WeakRef").ToString());
        Assert.Equal("function", ctx.Eval("typeof EventTarget").ToString());
        Assert.Equal("function", ctx.Eval("typeof FinalizationRegistry").ToString());
    }
}
