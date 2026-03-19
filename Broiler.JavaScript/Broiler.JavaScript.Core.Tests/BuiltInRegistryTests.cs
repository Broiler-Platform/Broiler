using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for the <see cref="IBuiltInRegistry"/> interface and
/// the <see cref="DefaultBuiltInRegistry"/> implementation.
/// Verifies that the registration mechanism is swappable and that
/// the default registry preserves existing behaviour.
/// </summary>
/// <remarks>
/// These tests modify the static <see cref="JSContext.BuiltInRegistry"/>
/// property, so they must not run in parallel with other test classes
/// that create <see cref="JSContext"/> instances.
/// </remarks>
[Collection("BuiltInRegistryTests")]
public class BuiltInRegistryTests : IDisposable
{
    private readonly IBuiltInRegistry _originalRegistry;

    public BuiltInRegistryTests()
    {
        // Capture the original registry so we can restore it in Dispose
        _originalRegistry = JSContext.BuiltInRegistry;
    }

    public void Dispose()
    {
        // Restore original registry to avoid side-effects across tests
        JSContext.BuiltInRegistry = _originalRegistry;
    }

    // ---------------------------------------------------------------
    // Default registry
    // ---------------------------------------------------------------

    [Fact]
    public void DefaultBuiltInRegistry_RegistersBuiltIns()
    {
        // The default registry should register all standard built-ins
        JSContext.BuiltInRegistry = DefaultBuiltInRegistry.Instance;
        using var ctx = new JSContext();
        JSContext.CurrentContext = ctx;

        // Verify core built-ins are available
        Assert.Equal("function", ctx.Eval("typeof Array").ToString());
        Assert.Equal("function", ctx.Eval("typeof String").ToString());
        Assert.Equal("function", ctx.Eval("typeof Number").ToString());
        Assert.Equal("function", ctx.Eval("typeof Date").ToString());
        Assert.Equal("function", ctx.Eval("typeof Promise").ToString());
        Assert.Equal("object", ctx.Eval("typeof Math").ToString());
        Assert.Equal("object", ctx.Eval("typeof JSON").ToString());
    }

    [Fact]
    public void DefaultBuiltInRegistry_Singleton_IsSameInstance()
    {
        var a = DefaultBuiltInRegistry.Instance;
        var b = DefaultBuiltInRegistry.Instance;
        Assert.Same(a, b);
    }

    // ---------------------------------------------------------------
    // Custom registry (swap)
    // ---------------------------------------------------------------

    [Fact]
    public void CustomRegistry_IsUsedByJSContext()
    {
        var called = false;
        JSContext.BuiltInRegistry = new DelegatingRegistry(() => called = true);

        using var ctx = new JSContext();
        Assert.True(called, "Custom IBuiltInRegistry.Register should be invoked by JSContext constructor");
    }

    [Fact]
    public void CustomRegistry_CanOmitBuiltIns()
    {
        // Register nothing — built-in constructors like Array will be undefined
        JSContext.BuiltInRegistry = new DelegatingRegistry(() => { });

        using var ctx = new JSContext();
        JSContext.CurrentContext = ctx;

        // Array, String, etc. should NOT be registered
        Assert.Equal("undefined", ctx.Eval("typeof Array").ToString());
        Assert.Equal("undefined", ctx.Eval("typeof String").ToString());
        Assert.Equal("undefined", ctx.Eval("typeof Number").ToString());
    }

    [Fact]
    public void CustomRegistry_CanRegisterSubset()
    {
        // Only register Array
        JSContext.BuiltInRegistry = new DelegatingRegistry(ctx =>
        {
            JSArray.CreateClass(ctx);
        });

        using var ctx = new JSContext();
        JSContext.CurrentContext = ctx;

        Assert.Equal("function", ctx.Eval("typeof Array").ToString());
        Assert.Equal("undefined", ctx.Eval("typeof Date").ToString());
        Assert.Equal("undefined", ctx.Eval("typeof Promise").ToString());
    }

    [Fact]
    public void BuiltInRegistry_DefaultIsDefaultBuiltInRegistry()
    {
        Assert.IsType<DefaultBuiltInRegistry>(JSContext.BuiltInRegistry);
    }

    // ---------------------------------------------------------------
    // Helper: delegating registry for testing
    // ---------------------------------------------------------------

    private sealed class DelegatingRegistry : IBuiltInRegistry
    {
        private readonly Action<JSContext>? _registerWithContext;
        private readonly Action? _registerSimple;

        public DelegatingRegistry(Action register)
        {
            _registerSimple = register;
        }

        public DelegatingRegistry(Action<JSContext> register)
        {
            _registerWithContext = register;
        }

        public void Register(JSContext context)
        {
            _registerWithContext?.Invoke(context);
            _registerSimple?.Invoke();
        }
    }
}
