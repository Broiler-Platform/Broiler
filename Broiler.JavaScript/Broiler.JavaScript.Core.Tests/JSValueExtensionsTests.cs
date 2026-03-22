using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for JSValueExtensions, particularly InvokeMethod overloads
/// that accept JSValue name parameters.
/// </summary>
public class JSValueExtensionsTests : IDisposable
{
    private readonly JSContext _context;

    public JSValueExtensionsTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void InvokeMethod_WithJSNumber_DoesNotThrowInvalidCastException()
    {
        // Arrange: create an array and use a JSNumber as the method name index
        // In JS: var arr = [function() { return 42; }]; arr[0]();
        var result = _context.Eval("var arr = [function() { return 42; }]; arr[0]()");
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void InvokeMethod_WithJSString_DoesNotThrowInvalidCastException()
    {
        // Arrange: invoke a method by string name via JSValue
        var result = _context.Eval("var obj = { hello: function() { return 'world'; } }; obj['hello']()");
        Assert.Equal("world", result.ToString());
    }

    [Fact]
    public void InvokeMethod_WithJSNumber_ViaExtension_DoesNotThrowInvalidCastException()
    {
        // Directly test the extension method with a JSNumber name
        // Create an object with a numeric property that is a function
        var obj = _context.Eval("(function() { var o = {}; o[0] = function() { return 99; }; return o; })()");
        JSValue name = new JSNumber(0);
        var result = obj.InvokeMethod(name);
        Assert.Equal(99d, result.DoubleValue);
    }

    [Fact]
    public void InvokeMethod_WithJSNumber_ThrowsTypeError_WhenMethodNotFound()
    {
        // When the property doesn't exist, should throw TypeError, not InvalidCastException
        var obj = _context.Eval("({})");
        JSValue name = new JSNumber(42);
        var ex = Assert.Throws<JSException>(() => obj.InvokeMethod(name));
        Assert.Contains("not found", ex.Message);
    }
}
