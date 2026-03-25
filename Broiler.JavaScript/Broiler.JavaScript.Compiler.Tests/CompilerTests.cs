using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Engine;

namespace Broiler.JavaScript.Compiler.Tests;

public class CompilerTests
{
    [Fact]
    public void Compile_SimpleExpression_ProducesResult()
    {
        using var ctx = new JSContext();
        var result = ctx.Eval("1 + 2 * 3");
        Assert.Equal(7.0, result.DoubleValue);
    }

    [Fact]
    public void Compile_LetConst_ScopingWorks()
    {
        using var ctx = new JSContext();
        var result = ctx.Eval("let x = 5; const y = 10; x + y;");
        Assert.Equal(15.0, result.DoubleValue);
    }

    [Fact]
    public void Compile_ArrowFunction_Works()
    {
        using var ctx = new JSContext();
        var result = ctx.Eval("((x) => x * 2)(21)");
        Assert.Equal(42.0, result.DoubleValue);
    }
}
