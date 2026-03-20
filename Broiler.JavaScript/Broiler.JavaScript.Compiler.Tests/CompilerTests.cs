using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.FastParser.Compiler;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Compiler.Tests;

/// <summary>
/// Tests for the <c>Broiler.JavaScript.Compiler</c> assembly, verifying
/// that the <see cref="FastCompiler"/> is correctly extracted and
/// functional via the delegate registration pattern.
/// </summary>
public class CompilerAssemblyTests : IDisposable
{
    private readonly JSContext _context;

    public CompilerAssemblyTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    // ---------------------------------------------------------------
    // Assembly initializer / registration
    // ---------------------------------------------------------------

    [Fact]
    public void DefaultJSCompiler_IsRegistered_AfterCompilerAssemblyLoads()
    {
        // After the Compiler assembly's module initializer runs,
        // DefaultJSCompiler should be able to compile without throwing.
        IJSCompiler compiler = new DefaultJSCompiler();
        var result = compiler.Compile("1 + 2;");
        Assert.NotNull(result);
    }

    [Fact]
    public void CoreScript_Compiler_DefaultIsDefaultJSCompiler()
    {
        Assert.IsType<DefaultJSCompiler>(CoreScript.Compiler);
    }

    // ---------------------------------------------------------------
    // FastCompiler – basic compilation
    // ---------------------------------------------------------------

    [Fact]
    public void FastCompiler_Compile_SimpleExpression()
    {
        var compiler = new FastCompiler("42;");
        Assert.NotNull(compiler.Method);
    }

    [Fact]
    public void FastCompiler_Compile_ArrowFunction()
    {
        var compiler = new FastCompiler("var f = (x) => x * 2;");
        Assert.NotNull(compiler.Method);
    }

    [Fact]
    public void FastCompiler_Compile_Class()
    {
        var compiler = new FastCompiler("class Foo { constructor() {} method() { return 1; } }");
        Assert.NotNull(compiler.Method);
    }

    // ---------------------------------------------------------------
    // End-to-end: compile + evaluate via CoreScript
    // ---------------------------------------------------------------

    [Fact]
    public void CoreScript_Evaluate_ReturnsResult()
    {
        var result = CoreScript.Evaluate("1 + 2;");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void CoreScript_Evaluate_String()
    {
        var result = CoreScript.Evaluate("'hello' + ' ' + 'world';");
        Assert.Equal("hello world", result.ToString());
    }

    [Fact]
    public void CoreScript_Evaluate_FunctionCall()
    {
        var result = CoreScript.Evaluate(@"
            function add(a, b) { return a + b; }
            add(3, 4);
        ");
        Assert.Equal(7d, result.DoubleValue);
    }

    [Fact]
    public void CoreScript_Evaluate_ArrayLiteral()
    {
        var result = CoreScript.Evaluate("[1, 2, 3].length;");
        Assert.Equal(3d, result.DoubleValue);
    }
}
