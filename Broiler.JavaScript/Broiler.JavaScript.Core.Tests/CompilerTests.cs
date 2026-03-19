using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Compiler;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for the expression compiler, verifying that JavaScript source code
/// is correctly compiled into expression trees via the <see cref="IJSCompiler"/>
/// and <see cref="FastCompiler"/> interfaces.
/// </summary>
public class CompilerTests
{
    /// <summary>
    /// Helper: compile source code using the <see cref="IJSCompiler"/> interface
    /// and return the resulting expression tree (non-null assertion included).
    /// </summary>
    private static YExpression<JSFunctionDelegate> CompileViaInterface(string code)
    {
        IJSCompiler compiler = new DefaultJSCompiler();
        var result = compiler.Compile(code);
        Assert.NotNull(result);
        return result;
    }

    /// <summary>
    /// Helper: compile source code directly using <see cref="FastCompiler"/>
    /// and return the resulting expression tree.
    /// </summary>
    private static YExpression<JSFunctionDelegate> CompileDirect(string code)
    {
        var compiler = new FastCompiler(code);
        Assert.NotNull(compiler.Method);
        return compiler.Method;
    }

    // ---------------------------------------------------------------
    // IJSCompiler interface tests
    // ---------------------------------------------------------------

    [Fact]
    public void IJSCompiler_DefaultImplementation_CompileSucceeds()
    {
        var method = CompileViaInterface("1 + 2;");
        Assert.NotNull(method);
    }

    [Fact]
    public void IJSCompiler_WithLocation_CompileSucceeds()
    {
        IJSCompiler compiler = new DefaultJSCompiler();
        var method = compiler.Compile("var x = 1;", "test.js");
        Assert.NotNull(method);
    }

    [Fact]
    public void IJSCompiler_WithArgs_CompileSucceeds()
    {
        IJSCompiler compiler = new DefaultJSCompiler();
        var method = compiler.Compile("return a + b;", "test.js", new List<string> { "a", "b" });
        Assert.NotNull(method);
    }

    // ---------------------------------------------------------------
    // Literal compilation
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_NumericLiteral()
    {
        CompileDirect("42;");
    }

    [Fact]
    public void Compile_StringLiteral()
    {
        CompileDirect("'hello';");
    }

    [Fact]
    public void Compile_BooleanLiterals()
    {
        CompileDirect("true; false;");
    }

    [Fact]
    public void Compile_NullLiteral()
    {
        CompileDirect("null;");
    }

    [Fact]
    public void Compile_UndefinedLiteral()
    {
        CompileDirect("undefined;");
    }

    // ---------------------------------------------------------------
    // Binary expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_Addition()
    {
        CompileDirect("1 + 2;");
    }

    [Fact]
    public void Compile_Subtraction()
    {
        CompileDirect("10 - 3;");
    }

    [Fact]
    public void Compile_Multiplication()
    {
        CompileDirect("6 * 7;");
    }

    [Fact]
    public void Compile_Division()
    {
        CompileDirect("20 / 4;");
    }

    [Fact]
    public void Compile_Modulo()
    {
        CompileDirect("10 % 3;");
    }

    [Fact]
    public void Compile_StrictEquality()
    {
        CompileDirect("1 === 1;");
    }

    [Fact]
    public void Compile_LogicalAnd()
    {
        CompileDirect("true && false;");
    }

    [Fact]
    public void Compile_LogicalOr()
    {
        CompileDirect("true || false;");
    }

    [Fact]
    public void Compile_BitwiseOperations()
    {
        CompileDirect("5 & 3; 5 | 3; 5 ^ 3; 5 << 1; 5 >> 1;");
    }

    // ---------------------------------------------------------------
    // Unary expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_UnaryNegation()
    {
        CompileDirect("-42;");
    }

    [Fact]
    public void Compile_UnaryNot()
    {
        CompileDirect("!true;");
    }

    [Fact]
    public void Compile_Typeof()
    {
        CompileDirect("typeof 42;");
    }

    [Fact]
    public void Compile_Void()
    {
        CompileDirect("void 0;");
    }

    // ---------------------------------------------------------------
    // Variable declarations
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_VarDeclaration()
    {
        CompileDirect("var x = 10;");
    }

    [Fact]
    public void Compile_LetDeclaration()
    {
        CompileDirect("let y = 20;");
    }

    [Fact]
    public void Compile_ConstDeclaration()
    {
        CompileDirect("const z = 30;");
    }

    [Fact]
    public void Compile_MultipleDeclarators()
    {
        CompileDirect("var a = 1, b = 2, c = 3;");
    }

    [Fact]
    public void Compile_Reassignment()
    {
        CompileDirect("var x = 1; x = 2;");
    }

    [Fact]
    public void Compile_CompoundAssignment()
    {
        CompileDirect("var x = 1; x += 10; x -= 5; x *= 2; x /= 3;");
    }

    // ---------------------------------------------------------------
    // Control flow — if/else
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_IfStatement()
    {
        CompileDirect("if (true) { 1; }");
    }

    [Fact]
    public void Compile_IfElseStatement()
    {
        CompileDirect("if (true) { 1; } else { 2; }");
    }

    [Fact]
    public void Compile_NestedIf()
    {
        CompileDirect("if (true) { if (false) { 1; } else { 2; } }");
    }

    // ---------------------------------------------------------------
    // Control flow — loops
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ForLoop()
    {
        CompileDirect("for (var i = 0; i < 10; i++) { i; }");
    }

    [Fact]
    public void Compile_WhileLoop()
    {
        CompileDirect("while (false) { 1; }");
    }

    [Fact]
    public void Compile_DoWhileLoop()
    {
        CompileDirect("do { 1; } while (false);");
    }

    [Fact]
    public void Compile_ForInLoop()
    {
        CompileDirect("var obj = {}; for (var k in obj) { k; }");
    }

    [Fact]
    public void Compile_ForOfLoop()
    {
        CompileDirect("for (var v of [1, 2, 3]) { v; }");
    }

    [Fact]
    public void Compile_BreakAndContinue()
    {
        CompileDirect("for (var i = 0; i < 10; i++) { if (i === 5) break; if (i === 3) continue; }");
    }

    // ---------------------------------------------------------------
    // Functions
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_FunctionDeclaration()
    {
        CompileDirect("function add(a, b) { return a + b; }");
    }

    [Fact]
    public void Compile_FunctionExpression()
    {
        CompileDirect("var f = function(x) { return x * 2; };");
    }

    [Fact]
    public void Compile_ArrowFunction()
    {
        CompileDirect("var f = (x) => x * 2;");
    }

    [Fact]
    public void Compile_ArrowFunction_ConciseBody()
    {
        CompileDirect("var f = x => x + 1;");
    }

    [Fact]
    public void Compile_ArrowFunction_Block()
    {
        CompileDirect("var f = (a, b) => { return a + b; };");
    }

    [Fact]
    public void Compile_AsyncFunction()
    {
        CompileDirect("async function fetchData() { return 1; }");
    }

    [Fact]
    public void Compile_RecursiveFunction()
    {
        CompileDirect(@"
            function factorial(n) {
                if (n <= 1) return 1;
                return n * factorial(n - 1);
            }
        ");
    }

    [Fact]
    public void Compile_Closure()
    {
        CompileDirect(@"
            function counter() {
                var count = 0;
                return function() { return ++count; };
            }
        ");
    }

    [Fact]
    public void Compile_DefaultParameters()
    {
        CompileDirect("function f(a, b = 10) { return a + b; }");
    }

    [Fact]
    public void Compile_RestParameters()
    {
        CompileDirect("function f(...args) { return args; }");
    }

    // ---------------------------------------------------------------
    // Return and throw
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ReturnStatement()
    {
        CompileDirect("function f() { return 42; }");
    }

    [Fact]
    public void Compile_ThrowStatement()
    {
        CompileDirect("function f() { throw new Error('fail'); }");
    }

    // ---------------------------------------------------------------
    // Try / catch / finally
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_TryCatch()
    {
        CompileDirect("try { 1; } catch(e) { 2; }");
    }

    [Fact]
    public void Compile_TryFinally()
    {
        CompileDirect("try { 1; } finally { 2; }");
    }

    [Fact]
    public void Compile_TryCatchFinally()
    {
        CompileDirect("try { 1; } catch(e) { 2; } finally { 3; }");
    }

    // ---------------------------------------------------------------
    // Classes
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ClassDeclaration()
    {
        CompileDirect("class Foo { constructor() {} method() { return 1; } }");
    }

    [Fact]
    public void Compile_ClassExtends()
    {
        CompileDirect("class Bar extends Foo { constructor() { super(); } }");
    }

    [Fact]
    public void Compile_ClassWithGetterSetter()
    {
        CompileDirect(@"
            class Obj {
                constructor() { this._v = 0; }
                get value() { return this._v; }
                set value(v) { this._v = v; }
            }
        ");
    }

    [Fact]
    public void Compile_ClassStaticMethod()
    {
        CompileDirect(@"
            class Util {
                static add(a, b) { return a + b; }
            }
        ");
    }

    // ---------------------------------------------------------------
    // Arrays
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ArrayLiteral()
    {
        CompileDirect("[1, 2, 3];");
    }

    [Fact]
    public void Compile_ArraySpread()
    {
        CompileDirect("[...[1, 2], 3];");
    }

    [Fact]
    public void Compile_ArrayDestructuring()
    {
        CompileDirect("var [a, b, c] = [1, 2, 3];");
    }

    // ---------------------------------------------------------------
    // Objects
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ObjectLiteral()
    {
        CompileDirect("var obj = {a: 1, b: 2};");
    }

    [Fact]
    public void Compile_ObjectComputedProperty()
    {
        CompileDirect("var key = 'x'; var obj = {[key]: 1};");
    }

    [Fact]
    public void Compile_ObjectShorthandMethod()
    {
        CompileDirect("var obj = { fn() { return 1; } };");
    }

    [Fact]
    public void Compile_ObjectDestructuring()
    {
        CompileDirect("var {x, y} = {x: 1, y: 2};");
    }

    [Fact]
    public void Compile_ObjectSpread()
    {
        CompileDirect("var a = {x: 1}; var b = {...a, y: 2};");
    }

    // ---------------------------------------------------------------
    // Member and call expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_MemberExpression()
    {
        CompileDirect("var obj = {a: 1}; obj.a;");
    }

    [Fact]
    public void Compile_ComputedMemberExpression()
    {
        CompileDirect("var obj = {}; obj['key'];");
    }

    [Fact]
    public void Compile_CallExpression()
    {
        CompileDirect("function f() {} f();");
    }

    [Fact]
    public void Compile_NewExpression()
    {
        CompileDirect("function Foo() {} new Foo();");
    }

    [Fact]
    public void Compile_ChainedCalls()
    {
        CompileDirect("var obj = { fn() { return this; } }; obj.fn().fn();");
    }

    // ---------------------------------------------------------------
    // Template literals
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_TemplateLiteral()
    {
        CompileDirect("var name = 'world'; `hello ${name}`;");
    }

    [Fact]
    public void Compile_TemplateMultiExpression()
    {
        CompileDirect("var a = 1; var b = 2; `${a} + ${b} = ${a + b}`;");
    }

    // ---------------------------------------------------------------
    // Switch statement
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_SwitchStatement()
    {
        CompileDirect("switch(1) { case 1: break; case 2: break; default: break; }");
    }

    // ---------------------------------------------------------------
    // Conditional (ternary)
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ConditionalExpression()
    {
        CompileDirect("true ? 1 : 2;");
    }

    [Fact]
    public void Compile_NestedConditional()
    {
        CompileDirect("true ? false ? 1 : 2 : 3;");
    }

    // ---------------------------------------------------------------
    // Sequence expressions
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_SequenceExpression()
    {
        CompileDirect("(1, 2, 3);");
    }

    // ---------------------------------------------------------------
    // Labeled statements
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_LabeledStatement()
    {
        CompileDirect("outer: for (var i = 0; i < 5; i++) { break outer; }");
    }

    // ---------------------------------------------------------------
    // Empty and minimal programs
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_EmptyProgram()
    {
        CompileDirect("");
    }

    [Fact]
    public void Compile_SingleSemicolon()
    {
        CompileDirect(";");
    }

    // ---------------------------------------------------------------
    // Complex / combined constructs
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_ComplexProgram()
    {
        CompileDirect(@"
            class Calculator {
                constructor(initial) {
                    this.value = initial || 0;
                }
                add(n) { this.value += n; return this; }
                subtract(n) { this.value -= n; return this; }
                result() { return this.value; }
            }

            function compute() {
                var calc = new Calculator(10);
                return calc.add(5).subtract(3).result();
            }
        ");
    }

    [Fact]
    public void Compile_NestedFunctions()
    {
        CompileDirect(@"
            function outer(x) {
                function inner(y) {
                    return x + y;
                }
                return inner(10);
            }
        ");
    }

    [Fact]
    public void Compile_MultipleFunctions()
    {
        CompileDirect(@"
            function add(a, b) { return a + b; }
            function mul(a, b) { return a * b; }
            var result = add(mul(2, 3), mul(4, 5));
        ");
    }

    // ---------------------------------------------------------------
    // Compilation error handling
    // ---------------------------------------------------------------

    [Fact]
    public void Compile_InvalidSyntax_Throws()
    {
        Assert.ThrowsAny<Exception>(() => CompileDirect("function { }"));
    }

    [Fact]
    public void Compile_UnexpectedToken_Throws()
    {
        Assert.ThrowsAny<Exception>(() => CompileDirect("var = ;"));
    }

    // ---------------------------------------------------------------
    // IParser interface tests
    // ---------------------------------------------------------------

    [Fact]
    public void IParser_FastParser_Implements_IParser()
    {
        var stream = new FastTokenStream("1 + 2;");
        var parser = new Broiler.JavaScript.Core.FastParser.FastParser(stream);
        Assert.IsAssignableFrom<IParser>(parser);
    }

    [Fact]
    public void IParser_ParseProgram_Via_Interface()
    {
        var stream = new FastTokenStream("var x = 42;");
        IParser parser = new Broiler.JavaScript.Core.FastParser.FastParser(stream);
        var program = parser.ParseProgram();
        Assert.NotNull(program);
        Assert.Equal(FastNodeType.Program, program.Type);
    }

    [Fact]
    public void IParser_ParseProgram_EmptySource()
    {
        var stream = new FastTokenStream("");
        IParser parser = new Broiler.JavaScript.Core.FastParser.FastParser(stream);
        var program = parser.ParseProgram();
        Assert.NotNull(program);
        Assert.Equal(0, program.Statements.Count);
    }

    [Fact]
    public void IParser_InvalidSyntax_Throws()
    {
        var stream = new FastTokenStream("function { }");
        IParser parser = new Broiler.JavaScript.Core.FastParser.FastParser(stream);
        Assert.ThrowsAny<Exception>(parser.ParseProgram);
    }
}
