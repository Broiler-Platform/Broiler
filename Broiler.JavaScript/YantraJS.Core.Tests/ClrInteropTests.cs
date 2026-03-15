using System;
using YantraJS.Core.Clr;

namespace YantraJS.Core.Tests;

/// <summary>
/// Tests for the <see cref="IClrInterop"/> interface and the
/// <see cref="DefaultClrInterop"/> implementation.
/// Verifies that the interop layer correctly marshals between .NET
/// and JavaScript values and that the interface is swappable via
/// <see cref="JSContext.ClrInterop"/>.
/// </summary>
[Collection("ClrInteropTests")]
public class ClrInteropTests : IDisposable
{
    private readonly JSContext _context;
    private readonly IClrInterop _originalInterop;

    public ClrInteropTests()
    {
        _originalInterop = JSContext.ClrInterop;
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
        JSContext.ClrInterop = _originalInterop;
    }

    // ---------------------------------------------------------------
    // IClrInterop contract
    // ---------------------------------------------------------------

    [Fact]
    public void IClrInterop_IsImplementedByDefaultClrInterop()
    {
        Assert.True(typeof(IClrInterop).IsAssignableFrom(typeof(DefaultClrInterop)));
    }

    [Fact]
    public void DefaultClrInterop_Singleton_IsSameInstance()
    {
        var a = DefaultClrInterop.Instance;
        var b = DefaultClrInterop.Instance;
        Assert.Same(a, b);
    }

    [Fact]
    public void JSContext_ClrInterop_DefaultIsDefaultClrInterop()
    {
        Assert.IsType<DefaultClrInterop>(JSContext.ClrInterop);
    }

    // ---------------------------------------------------------------
    // Marshal — primitives
    // ---------------------------------------------------------------

    [Fact]
    public void Marshal_Null_ReturnsJSNull()
    {
        var result = JSContext.ClrInterop.Marshal(null);
        Assert.True(result.IsNull);
    }

    [Fact]
    public void Marshal_Int_ReturnsJSNumber()
    {
        var result = JSContext.ClrInterop.Marshal(42);
        Assert.True(result.IsNumber);
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void Marshal_Double_ReturnsJSNumber()
    {
        var result = JSContext.ClrInterop.Marshal(3.14);
        Assert.True(result.IsNumber);
        Assert.Equal(3.14, result.DoubleValue);
    }

    [Fact]
    public void Marshal_String_ReturnsJSString()
    {
        var result = JSContext.ClrInterop.Marshal("hello");
        Assert.True(result.IsString);
        Assert.Equal("hello", result.ToString());
    }

    [Fact]
    public void Marshal_BoolTrue_ReturnsJSBooleanTrue()
    {
        var result = JSContext.ClrInterop.Marshal(true);
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Marshal_BoolFalse_ReturnsJSBooleanFalse()
    {
        var result = JSContext.ClrInterop.Marshal(false);
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Marshal_Long_ReturnsJSNumber()
    {
        var result = JSContext.ClrInterop.Marshal(100L);
        Assert.True(result.IsNumber);
        Assert.Equal(100d, result.DoubleValue);
    }

    [Fact]
    public void Marshal_Float_ReturnsJSNumber()
    {
        var result = JSContext.ClrInterop.Marshal(2.5f);
        Assert.True(result.IsNumber);
        Assert.Equal(2.5d, result.DoubleValue);
    }

    [Fact]
    public void Marshal_Byte_ReturnsJSNumber()
    {
        var result = JSContext.ClrInterop.Marshal((byte)255);
        Assert.True(result.IsNumber);
        Assert.Equal(255d, result.DoubleValue);
    }

    [Fact]
    public void Marshal_DateTime_ReturnsJSDate()
    {
        var dt = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var result = JSContext.ClrInterop.Marshal(dt);
        Assert.IsType<JSDate>(result);
    }

    // ---------------------------------------------------------------
    // Marshal — complex objects
    // ---------------------------------------------------------------

    [Fact]
    public void Marshal_ClrObject_ReturnsClrProxy()
    {
        var obj = new Uri("https://example.com");
        var result = JSContext.ClrInterop.Marshal(obj);
        Assert.IsType<ClrProxy>(result);
        Assert.Equal("https://example.com/", result.ToString());
    }

    [Fact]
    public void Marshal_Type_ReturnsClrType()
    {
        var result = JSContext.ClrInterop.Marshal(typeof(string));
        Assert.IsType<ClrType>(result);
    }

    // ---------------------------------------------------------------
    // GetClrType
    // ---------------------------------------------------------------

    [Fact]
    public void GetClrType_ReturnsClrTypeForSystemType()
    {
        var result = JSContext.ClrInterop.GetClrType(typeof(System.Text.StringBuilder));
        Assert.IsType<ClrType>(result);
    }

    [Fact]
    public void GetClrType_ReturnsSameInstanceForSameType()
    {
        var a = JSContext.ClrInterop.GetClrType(typeof(System.Guid));
        var b = JSContext.ClrInterop.GetClrType(typeof(System.Guid));
        Assert.Same(a, b);
    }

    // ---------------------------------------------------------------
    // Swappable interop
    // ---------------------------------------------------------------

    [Fact]
    public void CustomInterop_IsUsedViaClrInteropProperty()
    {
        var called = false;
        JSContext.ClrInterop = new DelegatingClrInterop(
            marshalFn: _ => { called = true; return JSNull.Value; });

        JSContext.ClrInterop.Marshal("test");
        Assert.True(called);
    }

    [Fact]
    public void CustomInterop_CanOverrideMarshal()
    {
        JSContext.ClrInterop = new DelegatingClrInterop(
            marshalFn: _ => new JSString("overridden"));

        var result = JSContext.ClrInterop.Marshal(42);
        Assert.Equal("overridden", result.ToString());
    }

    // ---------------------------------------------------------------
    // Helper: delegating interop
    // ---------------------------------------------------------------

    private sealed class DelegatingClrInterop : IClrInterop
    {
        private readonly Func<object, JSValue>? _marshalFn;
        private readonly Func<Type, JSValue>? _getClrTypeFn;

        public DelegatingClrInterop(
            Func<object, JSValue>? marshalFn = null,
            Func<Type, JSValue>? getClrTypeFn = null)
        {
            _marshalFn = marshalFn;
            _getClrTypeFn = getClrTypeFn;
        }

        public JSValue Marshal(object value) =>
            _marshalFn != null ? _marshalFn(value) : ClrProxy.Marshal(value);

        public JSValue GetClrType(Type type) =>
            _getClrTypeFn != null ? _getClrTypeFn(type) : ClrType.From(type);
    }
}
