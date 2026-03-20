using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.LinqExpressions;

namespace Broiler.JavaScript.Clr.Tests;

// ---------------------------------------------------------------
// ClrProxy tests
// ---------------------------------------------------------------

public class ClrProxyTests : IDisposable
{
    private readonly JSContext _context;

    public ClrProxyTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Marshal_Null_ReturnsJSNull()
    {
        var result = ClrProxy.Marshal((object)null);
        Assert.True(result.IsNull);
    }

    [Fact]
    public void Marshal_Int_ReturnsJSNumber()
    {
        var result = ClrProxy.Marshal(42);
        Assert.True(result.IsNumber);
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void Marshal_String_ReturnsJSString()
    {
        var result = ClrProxy.Marshal((object)"hello");
        Assert.True(result.IsString);
        Assert.Equal("hello", result.ToString());
    }

    [Fact]
    public void Marshal_Bool_ReturnsJSBoolean()
    {
        Assert.True(ClrProxy.Marshal(true).BooleanValue);
        Assert.False(ClrProxy.Marshal(false).BooleanValue);
    }

    [Fact]
    public void Marshal_ComplexObject_ReturnsClrProxy()
    {
        var uri = new Uri("https://example.com");
        var result = ClrProxy.Marshal(uri);
        Assert.IsType<ClrProxy>(result);
    }

    [Fact]
    public void Marshal_Type_ReturnsClrType()
    {
        var result = ClrProxy.Marshal(typeof(string));
        Assert.IsType<ClrType>(result);
    }

    [Fact]
    public void Marshal_JSValue_ReturnsAsIs()
    {
        JSValue jsStr = new JSString("test");
        var result = ClrProxy.Marshal(jsStr);
        Assert.Same(jsStr, result);
    }

    [Fact]
    public void From_Int_ReturnsClrProxy()
    {
        var result = ClrProxy.From(42);
        Assert.IsType<ClrProxy>(result);
    }

    [Fact]
    public void From_String_ReturnsClrProxy()
    {
        var result = ClrProxy.From("hello");
        Assert.IsType<ClrProxy>(result);
    }

    [Fact]
    public void From_Bool_ReturnsClrProxy()
    {
        var result = ClrProxy.From(true);
        Assert.IsType<ClrProxy>(result);
    }
}

// ---------------------------------------------------------------
// ClrType tests
// ---------------------------------------------------------------

public class ClrTypeTests : IDisposable
{
    private readonly JSContext _context;

    public ClrTypeTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void From_ReturnsClrType()
    {
        var result = ClrType.From(typeof(System.Text.StringBuilder));
        Assert.IsType<ClrType>(result);
    }

    [Fact]
    public void From_ReturnsSameInstanceForSameType()
    {
        var a = ClrType.From(typeof(Guid));
        var b = ClrType.From(typeof(Guid));
        Assert.Same(a, b);
    }

    [Fact]
    public void From_DifferentTypes_ReturnDifferentInstances()
    {
        var a = ClrType.From(typeof(int));
        var b = ClrType.From(typeof(string));
        Assert.NotSame(a, b);
    }

    [Fact]
    public void ClrType_IsJSFunction()
    {
        var ct = ClrType.From(typeof(object));
        Assert.IsAssignableFrom<JSFunction>(ct);
    }
}

// ---------------------------------------------------------------
// DefaultClrInterop tests
// ---------------------------------------------------------------

public class DefaultClrInteropTests : IDisposable
{
    private readonly JSContext _context;

    public DefaultClrInteropTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(DefaultClrInterop.Instance, DefaultClrInterop.Instance);
    }

    [Fact]
    public void ImplementsIClrInterop()
    {
        Assert.True(typeof(IClrInterop).IsAssignableFrom(typeof(DefaultClrInterop)));
    }

    [Fact]
    public void Marshal_Null_ReturnsJSNull()
    {
        Assert.True(DefaultClrInterop.Instance.Marshal(null).IsNull);
    }

    [Fact]
    public void Marshal_Primitive_ReturnsCorrectType()
    {
        var result = DefaultClrInterop.Instance.Marshal(42);
        Assert.True(result.IsNumber);
    }

    [Fact]
    public void Marshal_ComplexObject_ReturnsClrProxy()
    {
        var result = DefaultClrInterop.Instance.Marshal(new Uri("https://example.com"));
        Assert.IsType<ClrProxy>(result);
    }

    [Fact]
    public void GetClrType_ReturnsClrType()
    {
        var result = DefaultClrInterop.Instance.GetClrType(typeof(string));
        Assert.IsType<ClrType>(result);
    }

    [Fact]
    public void TryUnwrapClrObject_ClrProxy_ReturnsTrue()
    {
        var obj = new Uri("https://example.com");
        var proxy = ClrProxy.Marshal(obj);
        Assert.True(DefaultClrInterop.Instance.TryUnwrapClrObject(proxy, out var unwrapped));
        Assert.Same(obj, unwrapped);
    }

    [Fact]
    public void TryUnwrapClrObject_NonProxy_ReturnsFalse()
    {
        JSValue jsStr = new JSString("test");
        Assert.False(DefaultClrInterop.Instance.TryUnwrapClrObject(jsStr, out _));
    }
}

// ---------------------------------------------------------------
// ClrExpressionBuilder tests (via ClrProxyBuilder registration)
// ---------------------------------------------------------------

public class ClrExpressionBuilderTests
{
    [Fact]
    public void ClrProxyBuilder_MarshalRegistered()
    {
        // The module initializer should have registered the expression builder.
        // Verify by calling Marshal on a non-JSValue expression.
        var param = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression
            .Parameter(typeof(int), "x");

        // Should not throw — the builder should be registered.
        var result = ClrProxyBuilder.Marshal(param);
        Assert.NotNull(result);
    }

    [Fact]
    public void ClrProxyBuilder_FromRegistered()
    {
        var param = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression
            .Parameter(typeof(int), "x");

        var result = ClrProxyBuilder.From(param);
        Assert.NotNull(result);
    }

    [Fact]
    public void ClrProxyBuilder_Marshal_JSValuePassthrough()
    {
        var param = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression
            .Parameter(typeof(JSValue), "jsVal");

        // JSValue should be passed through without calling the builder.
        var result = ClrProxyBuilder.Marshal(param);
        Assert.Same(param, result);
    }
}

// ---------------------------------------------------------------
// Assembly initialization tests
// ---------------------------------------------------------------

public class ClrAssemblyInitializationTests : IDisposable
{
    private readonly JSContext _context;

    public ClrAssemblyInitializationTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void ClrInterop_IsDefaultClrInterop_WhenClrAssemblyLoaded()
    {
        // The Clr assembly's module initializer should have set this.
        Assert.IsType<DefaultClrInterop>(JSContext.ClrInterop);
    }

    [Fact]
    public void JSContext_CanBeCreated_WithClrInterop()
    {
        // Just verify a JSContext can be created and used normally.
        using var ctx = new JSContext();
        JSContext.CurrentContext = ctx;

        var result = ctx.Eval("1 + 2");
        Assert.Equal(3d, result.DoubleValue);
    }
}

// ---------------------------------------------------------------
// ClrModule tests
// ---------------------------------------------------------------

public class ClrModuleTests : IDisposable
{
    private readonly JSContext _context;

    public ClrModuleTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void ClrModule_Default_IsNotNull()
    {
        Assert.NotNull(ClrModule.Default);
    }

    [Fact]
    public void ClrModule_Default_IsJSObject()
    {
        Assert.IsAssignableFrom<JSValue>(ClrModule.Default);
    }
}
