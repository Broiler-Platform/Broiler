using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Parser;

namespace Broiler.JavaScript.Parser.Tests;

/// <summary>
/// Tests for <see cref="FastParser"/> — verifies that JavaScript source
/// code is correctly parsed into AST nodes.
/// </summary>
public class FastParserTests
{
    private static AstProgram Parse(string code)
    {
        var stream = new FastTokenStream(code);
        var parser = new FastParser(stream);
        return parser.ParseProgram();
    }

    // ---------------------------------------------------------------
    // Literals
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_NumericLiteral()
    {
        var program = Parse("42;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ExpressionStatement, stmt.Type);
    }

    [Fact]
    public void Parse_StringLiteral()
    {
        var program = Parse("\"hello\";");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ExpressionStatement, stmt.Type);
    }

    [Fact]
    public void Parse_BooleanLiteral_True()
    {
        var program = Parse("true;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_BooleanLiteral_False()
    {
        var program = Parse("false;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_NullLiteral()
    {
        var program = Parse("null;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Variable declarations
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_VarDeclaration()
    {
        var program = Parse("var x = 1;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.VariableDeclaration, stmt.Type);
    }

    [Fact]
    public void Parse_LetDeclaration()
    {
        var program = Parse("let x = 1;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.VariableDeclaration, stmt.Type);
    }

    [Fact]
    public void Parse_ConstDeclaration()
    {
        var program = Parse("const x = 1;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.VariableDeclaration, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Control flow
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_IfStatement()
    {
        var program = Parse("if (true) { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.IfStatement, stmt.Type);
    }

    [Fact]
    public void Parse_IfElseStatement()
    {
        var program = Parse("if (true) { } else { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.IfStatement, stmt.Type);
    }

    [Fact]
    public void Parse_ForStatement()
    {
        var program = Parse("for (var i = 0; i < 10; i++) { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ForStatement, stmt.Type);
    }

    [Fact]
    public void Parse_WhileStatement()
    {
        var program = Parse("while (true) { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.WhileStatement, stmt.Type);
    }

    [Fact]
    public void Parse_DoWhileStatement()
    {
        var program = Parse("do { } while (true);");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.DoWhileStatement, stmt.Type);
    }

    [Fact]
    public void Parse_ForInStatement()
    {
        var program = Parse("for (var x in obj) { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ForInStatement, stmt.Type);
    }

    [Fact]
    public void Parse_ForOfStatement()
    {
        var program = Parse("for (var x of arr) { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ForOfStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Functions
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_FunctionDeclaration()
    {
        var program = Parse("function foo() { return 1; }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ArrowFunction()
    {
        var program = Parse("const f = () => 1;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ArrowFunction_WithBody()
    {
        var program = Parse("const f = (x) => { return x + 1; };");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_BinaryExpression()
    {
        var program = Parse("1 + 2;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_MemberExpression()
    {
        var program = Parse("a.b;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_CallExpression()
    {
        var program = Parse("foo();");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ArrayExpression()
    {
        var program = Parse("[1, 2, 3];");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ObjectExpression()
    {
        var program = Parse("({a: 1, b: 2});");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_TemplateLiteral()
    {
        var program = Parse("`hello ${name}`;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Classes
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ClassDeclaration()
    {
        var program = Parse("class Foo { }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ClassWithConstructor()
    {
        var program = Parse("class Foo { constructor() { } }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ClassExtends()
    {
        var program = Parse("class Bar extends Foo { }");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Try/catch/finally
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_TryCatch()
    {
        var program = Parse("try { } catch (e) { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.TryStatement, stmt.Type);
    }

    [Fact]
    public void Parse_TryFinally()
    {
        var program = Parse("try { } finally { }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.TryStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Statements
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ReturnStatement()
    {
        var program = Parse("function f() { return 42; }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ThrowStatement()
    {
        var program = Parse("throw new Error();");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ThrowStatement, stmt.Type);
    }

    [Fact]
    public void Parse_DebuggerStatement()
    {
        var program = Parse("debugger;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.DebuggerStatement, stmt.Type);
    }

    [Fact]
    public void Parse_SwitchStatement()
    {
        var program = Parse("switch (x) { case 1: break; default: break; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.SwitchStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Multiple statements
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_MultipleStatements()
    {
        var program = Parse("var x = 1; var y = 2;");
        Assert.Equal(2, program.Statements.Count);
    }

    // ---------------------------------------------------------------
    // Error handling
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_EmptyProgram()
    {
        var program = Parse("");
        Assert.Empty(program.Statements);
    }

    [Fact]
    public void Parse_InvalidSyntax_Throws()
    {
        Assert.Throws<FastParseException>(() => Parse("var = ;"));
    }

    // ---------------------------------------------------------------
    // ES2015+ features
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_Destructuring_Array()
    {
        var program = Parse("const [a, b] = [1, 2];");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_Destructuring_Object()
    {
        var program = Parse("const { a, b } = obj;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_SpreadOperator()
    {
        var program = Parse("[...arr];");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_AsyncFunction()
    {
        var program = Parse("async function f() { await 1; }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_GeneratorFunction()
    {
        var program = Parse("function* gen() { yield 1; }");
        Assert.Single(program.Statements);
    }
}
