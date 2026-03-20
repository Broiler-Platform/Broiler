using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Debugger;

namespace Broiler.JavaScript.Debugger.Tests;

/// <summary>
/// Tests for <see cref="V8RemoteObject"/>.
/// </summary>
public class V8RemoteObjectTests
{
    [Fact]
    public void Constructor_String_SetsTypeAndValue()
    {
        var ro = new V8RemoteObject("hello");

        Assert.Equal("string", ro.Type);
        Assert.Equal("hello", ro.Value);
    }

    [Fact]
    public void Constructor_JSUndefined_SetsUndefinedType()
    {
        var ro = new V8RemoteObject(JSUndefined.Value);

        Assert.Equal("undefined", ro.Type);
        Assert.Equal("undefined", ro.Description);
    }

    [Fact]
    public void Constructor_JSNull_SetsObjectType()
    {
        var ro = new V8RemoteObject(JSNull.Value);

        Assert.Equal("object", ro.Type);
        Assert.Equal("null", ro.Value);
        Assert.Equal("null", ro.Description);
    }

    [Fact]
    public void Constructor_JSString_SetsStringType()
    {
        var jsStr = new JSString("test");
        var ro = new V8RemoteObject(jsStr);

        Assert.Equal("string", ro.Type);
        Assert.Equal("test", ro.Value);
    }

    [Fact]
    public void Constructor_JSNumber_SetsNumberType()
    {
        var jsNum = new JSNumber(42.0);
        var ro = new V8RemoteObject(jsNum);

        Assert.Equal("number", ro.Type);
        Assert.Equal(42.0, ro.Value);
    }

    [Fact]
    public void Constructor_JSBoolean_SetsBooleanType()
    {
        var jsBool = JSBoolean.True;
        var ro = new V8RemoteObject(jsBool);

        Assert.Equal("boolean", ro.Type);
        Assert.Equal(true, ro.Value);
    }
}
