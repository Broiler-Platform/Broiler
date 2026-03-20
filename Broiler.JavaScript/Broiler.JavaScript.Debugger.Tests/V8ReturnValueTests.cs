using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Debugger;

namespace Broiler.JavaScript.Debugger.Tests;

/// <summary>
/// Tests for <see cref="V8ReturnValue"/>.
/// </summary>
public class V8ReturnValueTests
{
    [Fact]
    public void DefaultConstructor_HasNullProperties()
    {
        var rv = new V8ReturnValue();

        Assert.Null(rv.ExceptionDetails);
        Assert.Null(rv.ScriptId);
        Assert.Null(rv.Result);
        Assert.Null(rv.Id);
        Assert.Null(rv.ScriptSource);
    }

    [Fact]
    public void Constructor_WithException_SetsExceptionDetails()
    {
        var ex = new InvalidOperationException("test error");
        var rv = new V8ReturnValue(ex);

        Assert.NotNull(rv.ExceptionDetails);
        Assert.Contains("test error", rv.ExceptionDetails.Text);
    }

    [Fact]
    public void ImplicitConversion_FromException()
    {
        V8ReturnValue rv = new InvalidOperationException("implicit");

        Assert.NotNull(rv.ExceptionDetails);
        Assert.Contains("implicit", rv.ExceptionDetails.Text);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var rv = new V8ReturnValue
        {
            ScriptId = "s1",
            Id = "id1",
            ScriptSource = "var x = 1;"
        };

        Assert.Equal("s1", rv.ScriptId);
        Assert.Equal("id1", rv.Id);
        Assert.Equal("var x = 1;", rv.ScriptSource);
    }
}
