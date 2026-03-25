using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Engine;


namespace Broiler.JavaScript.Core.Tests;

public class CoreTests
{
    [Fact]
    public void JSContext_Create_And_Dispose()
    {
        using var ctx = new JSContext();
        Assert.NotNull(ctx);
    }

    [Fact]
    public void JSContext_Eval_SimpleArithmetic()
    {
        using var ctx = new JSContext();
        var result = ctx.Eval("2 + 3");
        Assert.Equal(5.0, result.DoubleValue);
    }

    [Fact]
    public void JSContext_Eval_StringConcatenation()
    {
        using var ctx = new JSContext();
        var result = ctx.Eval("'hello' + ' ' + 'world'");
        Assert.Equal("hello world", result.ToString());
    }

    [Fact]
    public void JSNumber_StoresValue()
    {
        var num = new JSNumber(42);
        Assert.Equal(42.0, num.DoubleValue);
        Assert.True(num.IsNumber);
    }

    [Fact]
    public void JSString_StoresValue()
    {
        var str = new JSString("test");
        Assert.Equal("test", str.ToString());
        Assert.True(str.IsString);
    }

    [Fact]
    public void JSContext_Eval_VariableDeclaration()
    {
        using var ctx = new JSContext();
        ctx.Eval("var x = 10;");
        var result = ctx.Eval("x * 2");
        Assert.Equal(20.0, result.DoubleValue);
    }

    [Fact]
    public void JSContext_Eval_FunctionCall()
    {
        using var ctx = new JSContext();
        var result = ctx.Eval("(function(a, b) { return a + b; })(3, 4)");
        Assert.Equal(7.0, result.DoubleValue);
    }
}
