using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Debugger;
using Broiler.JavaScript.Engine;

namespace Broiler.JavaScript.Debugger.Tests;

public class DebuggerTests
{
    [Fact]
    public void V8RemoteObject_FromNumber_HasNumberType()
    {
        using var ctx = new JSContext();
        var num = new JSNumber(42);
        var remote = new V8RemoteObject(num);
        Assert.Equal("number", remote.Type);
    }

    [Fact]
    public void V8RemoteObject_FromString_HasStringType()
    {
        var remote = new V8RemoteObject("test");
        Assert.Equal("string", remote.Type);
    }

    [Fact]
    public void V8CallFrame_DefaultValues()
    {
        var frame = new V8CallFrame();
        Assert.Equal(0, frame.ColumnNumber);
        Assert.Equal(0, frame.LineNumber);
    }
}
