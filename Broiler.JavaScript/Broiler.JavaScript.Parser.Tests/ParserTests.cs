using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Parser;

namespace Broiler.JavaScript.Parser.Tests;

public class ParserTests
{
    [Fact]
    public void ParseProgram_SimpleExpression_ReturnsProgram()
    {
        var stream = new FastTokenStream(new StringSpan("42;"));
        var parser = new FastParser(stream);
        var program = parser.ParseProgram();
        Assert.NotNull(program);
        Assert.Equal(FastNodeType.Program, program.Type);
    }

    [Fact]
    public void ParseProgram_VariableDeclaration_Succeeds()
    {
        var stream = new FastTokenStream(new StringSpan("var x = 10;"));
        var parser = new FastParser(stream);
        var program = parser.ParseProgram();
        Assert.NotNull(program);
    }

    [Fact]
    public void ParseProgram_FunctionDeclaration_Succeeds()
    {
        var stream = new FastTokenStream(new StringSpan("function add(a, b) { return a + b; }"));
        var parser = new FastParser(stream);
        var program = parser.ParseProgram();
        Assert.NotNull(program);
    }

    [Fact]
    public void ParseProgram_ArrowFunction_Succeeds()
    {
        var stream = new FastTokenStream(new StringSpan("const f = (x) => x * 2;"));
        var parser = new FastParser(stream);
        var program = parser.ParseProgram();
        Assert.NotNull(program);
    }

    [Fact]
    public void ParseProgram_InvalidSyntax_ThrowsFastParseException()
    {
        var stream = new FastTokenStream(new StringSpan("function { }"));
        var parser = new FastParser(stream);
        Assert.Throws<FastParseException>(() => parser.ParseProgram());
    }
}
