using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Debugger;

namespace Broiler.JavaScript.Debugger.Tests;

/// <summary>
/// Tests for <see cref="V8ExceptionDetails"/>.
/// </summary>
public class V8ExceptionDetailsTests
{
    [Fact]
    public void Constructor_WithException_SetsText()
    {
        var ex = new ArgumentException("bad arg");
        var details = new V8ExceptionDetails(ex);

        Assert.Contains("bad arg", details.Text);
    }

    [Fact]
    public void Constructor_WithoutContext_HasNullStackTrace()
    {
        var ex = new Exception("test");
        var details = new V8ExceptionDetails(ex);

        Assert.Null(details.StackTrace);
        Assert.Null(details.ExecutionContextId);
    }

    [Fact]
    public void Constructor_WithJSException_SetsRemoteObject()
    {
        var jsEx = new JSException(new JSString("JS error"));
        var details = new V8ExceptionDetails(jsEx);

        Assert.NotNull(details.Exception);
        Assert.Equal("string", details.Exception.Type);
    }

    [Fact]
    public void Properties_AreDefaultNull()
    {
        var details = new V8ExceptionDetails(new Exception("x"));

        Assert.Equal(0, details.ExceptionId);
        Assert.Equal(0, details.LineNumber);
        Assert.Equal(0, details.ColumnNumber);
        Assert.Null(details.ScriptId);
        Assert.Null(details.Url);
    }
}
