using Broiler.JavaScript.Debugger;

namespace Broiler.JavaScript.Debugger.Tests;

/// <summary>
/// Tests for <see cref="V8CallFrame"/>.
/// </summary>
public class V8CallFrameTests
{
    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var frame = new V8CallFrame
        {
            FunctionName = "myFunc",
            ScriptId = "script1",
            Url = "file:///test.js",
            LineNumber = 10,
            ColumnNumber = 5
        };

        Assert.Equal("myFunc", frame.FunctionName);
        Assert.Equal("script1", frame.ScriptId);
        Assert.Equal("file:///test.js", frame.Url);
        Assert.Equal(10, frame.LineNumber);
        Assert.Equal(5, frame.ColumnNumber);
    }

    [Fact]
    public void Default_HasNullProperties()
    {
        var frame = new V8CallFrame();

        Assert.Null(frame.FunctionName);
        Assert.Null(frame.ScriptId);
        Assert.Null(frame.Url);
        Assert.Equal(0, frame.LineNumber);
        Assert.Equal(0, frame.ColumnNumber);
    }
}
