using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Parser;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for the FastParser, verifying AST output for representative
/// JavaScript constructs: expressions, statements, functions, and classes.
/// </summary>
public class ParserTests
{
    private static AstProgram Parse(string code)
    {
        var stream = new FastTokenStream(code);
        var parser = new Broiler.JavaScript.Parser.FastParser(stream);
        return parser.ParseProgram();
    }

    // ---------------------------------------------------------------
    // Numeric and string literals
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
        var program = Parse("'hello';");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ExpressionStatement, stmt.Type);
    }

    [Fact]
    public void Parse_BooleanLiterals()
    {
        var program = Parse("true; false;");
        Assert.Equal(2, program.Statements.Count);
    }

    [Fact]
    public void Parse_NullLiteral()
    {
        var program = Parse("null;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Binary expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_BinaryExpression_Addition()
    {
        var program = Parse("1 + 2;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ExpressionStatement, stmt.Type);
    }

    [Fact]
    public void Parse_BinaryExpression_Comparison()
    {
        var program = Parse("x === 5;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_BinaryExpression_LogicalAnd()
    {
        var program = Parse("a && b;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_BinaryExpression_LogicalOr()
    {
        var program = Parse("a || b;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Unary expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_UnaryExpression_Negation()
    {
        var program = Parse("-x;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_UnaryExpression_Typeof()
    {
        var program = Parse("typeof x;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_UnaryExpression_Not()
    {
        var program = Parse("!true;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Variable declarations
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_VarDeclaration()
    {
        var program = Parse("var x = 10;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.VariableDeclaration, stmt.Type);
    }

    [Fact]
    public void Parse_LetDeclaration()
    {
        var program = Parse("let y = 20;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.VariableDeclaration, stmt.Type);
    }

    [Fact]
    public void Parse_ConstDeclaration()
    {
        var program = Parse("const z = 30;");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.VariableDeclaration, stmt.Type);
    }

    [Fact]
    public void Parse_MultipleDeclarators()
    {
        var program = Parse("var a = 1, b = 2;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // If statements
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_IfStatement()
    {
        var program = Parse("if (x > 0) { y = 1; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.IfStatement, stmt.Type);
    }

    [Fact]
    public void Parse_IfElseStatement()
    {
        var program = Parse("if (x) { a; } else { b; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.IfStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // For / while loops
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ForLoop()
    {
        var program = Parse("for (var i = 0; i < 10; i++) { x; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ForStatement, stmt.Type);
    }

    [Fact]
    public void Parse_WhileLoop()
    {
        var program = Parse("while (x) { y; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.WhileStatement, stmt.Type);
    }

    [Fact]
    public void Parse_DoWhileLoop()
    {
        var program = Parse("do { x; } while (y);");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.DoWhileStatement, stmt.Type);
    }

    [Fact]
    public void Parse_ForInLoop()
    {
        var program = Parse("for (var k in obj) { x; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ForInStatement, stmt.Type);
    }

    [Fact]
    public void Parse_ForOfLoop()
    {
        var program = Parse("for (const v of arr) { x; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ForOfStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Functions
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_FunctionDeclaration()
    {
        var program = Parse("function add(a, b) { return a + b; }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ArrowFunction()
    {
        var program = Parse("const f = (x) => x * 2;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ArrowFunction_ConciseBody()
    {
        var program = Parse("const f = x => x + 1;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_FunctionExpression()
    {
        var program = Parse("var f = function(x) { return x; };");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_AsyncFunction()
    {
        var program = Parse("async function fetchData() { return 1; }");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Return, throw, try/catch
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
        var program = Parse("throw new Error('fail');");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.ThrowStatement, stmt.Type);
    }

    [Fact]
    public void Parse_TryCatchFinally()
    {
        var program = Parse("try { x; } catch(e) { y; } finally { z; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.TryStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Classes
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ClassDeclaration()
    {
        var program = Parse("class Foo { constructor() {} method() { return 1; } }");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ClassExtends()
    {
        var program = Parse("class Bar extends Foo { constructor() { super(); } }");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Array and object literals
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ArrayLiteral()
    {
        var program = Parse("[1, 2, 3];");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ObjectLiteral()
    {
        var program = Parse("({a: 1, b: 2});");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Call and member expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_CallExpression()
    {
        var program = Parse("foo(1, 2);");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_MemberExpression()
    {
        var program = Parse("obj.prop;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ComputedMemberExpression()
    {
        var program = Parse("obj['key'];");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_NewExpression()
    {
        var program = Parse("new Foo(1);");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Template literals
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_TemplateLiteral()
    {
        var program = Parse("`hello ${name}`;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Switch statement
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_SwitchStatement()
    {
        var program = Parse("switch(x) { case 1: break; default: break; }");
        var stmt = Assert.Single(program.Statements);
        Assert.Equal(FastNodeType.SwitchStatement, stmt.Type);
    }

    // ---------------------------------------------------------------
    // Spread and rest
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_SpreadElement()
    {
        var program = Parse("foo(...args);");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_RestParameter()
    {
        var program = Parse("function f(...args) { return args; }");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Destructuring
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ArrayDestructuring()
    {
        var program = Parse("const [a, b] = [1, 2];");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_ObjectDestructuring()
    {
        var program = Parse("const {x, y} = obj;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Conditional (ternary)
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_ConditionalExpression()
    {
        var program = Parse("x ? 1 : 2;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Assignment
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_Assignment()
    {
        var program = Parse("x = 5;");
        Assert.Single(program.Statements);
    }

    [Fact]
    public void Parse_CompoundAssignment()
    {
        var program = Parse("x += 10;");
        Assert.Single(program.Statements);
    }

    // ---------------------------------------------------------------
    // Empty program
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_EmptyProgram()
    {
        var program = Parse("");
        Assert.Equal(0, program.Statements.Count);
    }

    // ---------------------------------------------------------------
    // Multi-statement program
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_MultipleStatements()
    {
        var program = Parse("var x = 1; var y = 2; var z = x + y;");
        Assert.Equal(3, program.Statements.Count);
    }

    // ---------------------------------------------------------------
    // Error handling
    // ---------------------------------------------------------------

    [Fact]
    public void Parse_InvalidSyntax_Throws()
    {
        Assert.ThrowsAny<Exception>(() => Parse("function { }"));
    }

    [Fact]
    public void Parse_UnexpectedToken_Throws()
    {
        Assert.ThrowsAny<Exception>(() => Parse("var = ;"));
    }
}
