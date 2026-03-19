using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for the Broiler.JavaScript runtime: JSContext lifecycle, JSValue type system,
/// type coercion, and end-to-end script evaluation.
/// </summary>
public class RuntimeTests : IDisposable
{
    private readonly JSContext _context;

    public RuntimeTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // JSContext lifecycle
    // ---------------------------------------------------------------

    [Fact]
    public void JSContext_Creates_Successfully()
    {
        Assert.NotNull(_context);
        Assert.True(_context.ID > 0);
    }

    [Fact]
    public void JSContext_Current_Is_Set()
    {
        Assert.Same(_context, JSContext.Current);
    }

    [Fact]
    public void JSContext_Has_ObjectPrototype()
    {
        Assert.NotNull(_context.ObjectPrototype);
    }

    [Fact]
    public void JSContext_Multiple_Instances_Have_Unique_IDs()
    {
        using var ctx2 = new JSContext();
        Assert.NotEqual(_context.ID, ctx2.ID);
    }

    // ---------------------------------------------------------------
    // Eval — arithmetic
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_Addition()
    {
        var result = _context.Eval("1 + 2");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Eval_Subtraction()
    {
        var result = _context.Eval("10 - 3");
        Assert.Equal(7d, result.DoubleValue);
    }

    [Fact]
    public void Eval_Multiplication()
    {
        var result = _context.Eval("6 * 7");
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void Eval_Division()
    {
        var result = _context.Eval("20 / 4");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Eval_Modulo()
    {
        var result = _context.Eval("10 % 3");
        Assert.Equal(1d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — string operations
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_StringConcatenation()
    {
        var result = _context.Eval("'hello' + ' ' + 'world'");
        Assert.Equal("hello world", result.ToString());
    }

    [Fact]
    public void Eval_StringLength()
    {
        var result = _context.Eval("'test'.length");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Eval_TemplateLiteral()
    {
        var result = _context.Eval("var x = 42; `value is ${x}`");
        Assert.Equal("value is 42", result.ToString());
    }

    // ---------------------------------------------------------------
    // Eval — boolean and comparison
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_BooleanTrue()
    {
        var result = _context.Eval("true");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Eval_BooleanFalse()
    {
        var result = _context.Eval("false");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Eval_StrictEquality()
    {
        var result = _context.Eval("1 === 1");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Eval_StrictInequality()
    {
        var result = _context.Eval("1 === '1'");
        Assert.False(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // Eval — null and undefined
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_Undefined()
    {
        var result = _context.Eval("undefined");
        Assert.True(result.IsUndefined);
    }

    [Fact]
    public void Eval_Null()
    {
        var result = _context.Eval("null");
        Assert.True(result.IsNull);
    }

    // ---------------------------------------------------------------
    // Eval — variables and scoping
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_VarDeclaration_And_Access()
    {
        var result = _context.Eval("var x = 100; x");
        Assert.Equal(100d, result.DoubleValue);
    }

    [Fact]
    public void Eval_LetDeclaration()
    {
        var result = _context.Eval("let y = 200; y");
        Assert.Equal(200d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ConstDeclaration()
    {
        var result = _context.Eval("const z = 300; z");
        Assert.Equal(300d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — control flow
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_IfStatement()
    {
        var result = _context.Eval("var r; if (true) { r = 1; } else { r = 2; } r");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ForLoop()
    {
        var result = _context.Eval("var sum = 0; for (var i = 1; i <= 5; i++) { sum += i; } sum");
        Assert.Equal(15d, result.DoubleValue);
    }

    [Fact]
    public void Eval_WhileLoop()
    {
        var result = _context.Eval("var n = 0; while (n < 3) { n++; } n");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Eval_TernaryOperator()
    {
        var result = _context.Eval("true ? 'yes' : 'no'");
        Assert.Equal("yes", result.ToString());
    }

    // ---------------------------------------------------------------
    // Eval — functions
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_FunctionDeclaration_And_Call()
    {
        var result = _context.Eval("function add(a, b) { return a + b; } add(3, 4)");
        Assert.Equal(7d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ArrowFunction()
    {
        var result = _context.Eval("var mul = (a, b) => a * b; mul(5, 6)");
        Assert.Equal(30d, result.DoubleValue);
    }

    [Fact]
    public void Eval_RecursiveFunction()
    {
        var result = _context.Eval(@"
            function factorial(n) {
                if (n <= 1) return 1;
                return n * factorial(n - 1);
            }
            factorial(5)
        ");
        Assert.Equal(120d, result.DoubleValue);
    }

    [Fact]
    public void Eval_Closure()
    {
        var result = _context.Eval(@"
            function counter() {
                var count = 0;
                return function() { return ++count; };
            }
            var inc = counter();
            inc(); inc(); inc()
        ");
        Assert.Equal(3d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — objects
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_ObjectLiteral()
    {
        var result = _context.Eval("var obj = {a: 1, b: 2}; obj.a + obj.b");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ObjectComputedProperty()
    {
        var result = _context.Eval("var obj = {}; obj['key'] = 'value'; obj['key']");
        Assert.Equal("value", result.ToString());
    }

    // ---------------------------------------------------------------
    // Eval — arrays
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_ArrayLiteral()
    {
        var result = _context.Eval("[1, 2, 3].length");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ArrayPush()
    {
        var result = _context.Eval("var arr = []; arr.push(10); arr[0]");
        Assert.Equal(10d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ArrayMap()
    {
        var result = _context.Eval("[1,2,3].map(x => x * 2)[1]");
        Assert.Equal(4d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — typeof
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_TypeofNumber()
    {
        var result = _context.Eval("typeof 42");
        Assert.Equal("number", result.ToString());
    }

    [Fact]
    public void Eval_TypeofString()
    {
        var result = _context.Eval("typeof 'hello'");
        Assert.Equal("string", result.ToString());
    }

    [Fact]
    public void Eval_TypeofObject()
    {
        var result = _context.Eval("typeof {}");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Eval_TypeofFunction()
    {
        var result = _context.Eval("typeof function(){}");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Eval_TypeofUndefined()
    {
        var result = _context.Eval("typeof undefined");
        Assert.Equal("undefined", result.ToString());
    }

    // ---------------------------------------------------------------
    // Eval — error handling
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_TryCatch()
    {
        var result = _context.Eval(@"
            var result;
            try { throw new Error('test'); }
            catch(e) { result = e.message; }
            result
        ");
        Assert.Equal("test", result.ToString());
    }

    [Fact]
    public void Eval_SyntaxError_Throws()
    {
        Assert.ThrowsAny<Exception>(() => _context.Eval("function { }"));
    }

    // ---------------------------------------------------------------
    // Eval — classes
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_ClassInstantiation()
    {
        var result = _context.Eval(@"
            class Point {
                constructor(x, y) {
                    this.x = x;
                    this.y = y;
                }
                sum() { return this.x + this.y; }
            }
            var p = new Point(3, 4);
            p.sum()
        ");
        Assert.Equal(7d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ClassInheritance()
    {
        var result = _context.Eval(@"
            class Animal {
                constructor(name) { this.name = name; }
                speak() { return this.name; }
            }
            class Dog extends Animal {
                constructor(name) { super(name); }
                speak() { return super.speak() + ' barks'; }
            }
            new Dog('Rex').speak()
        ");
        Assert.Equal("Rex barks", result.ToString());
    }

    // ---------------------------------------------------------------
    // JSValue type checks
    // ---------------------------------------------------------------

    [Fact]
    public void JSValue_Number_Properties()
    {
        var result = _context.Eval("42");
        Assert.True(result.IsNumber);
        Assert.False(result.IsString);
        Assert.False(result.IsUndefined);
        Assert.False(result.IsNull);
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void JSValue_String_Properties()
    {
        var result = _context.Eval("'test'");
        Assert.True(result.IsString);
        Assert.False(result.IsNumber);
        Assert.Equal("test", result.ToString());
    }

    [Fact]
    public void JSValue_Boolean_Coercion()
    {
        Assert.True(_context.Eval("1").BooleanValue);
        Assert.False(_context.Eval("0").BooleanValue);
        Assert.True(_context.Eval("'non-empty'").BooleanValue);
        Assert.False(_context.Eval("''").BooleanValue);
    }

    // ---------------------------------------------------------------
    // Eval — built-in methods
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_MathMax()
    {
        var result = _context.Eval("Math.max(1, 5, 3)");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Eval_MathFloor()
    {
        var result = _context.Eval("Math.floor(4.7)");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ParseInt()
    {
        var result = _context.Eval("parseInt('42')");
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void Eval_JSONStringify()
    {
        var result = _context.Eval("JSON.stringify({a:1})");
        Assert.Equal("{\"a\":1}", result.ToString());
    }

    [Fact]
    public void Eval_JSONParse()
    {
        var result = _context.Eval("JSON.parse('{\"x\":10}').x");
        Assert.Equal(10d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — spread and rest
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_SpreadArray()
    {
        var result = _context.Eval("[...[1,2], ...[3,4]].length");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Eval_RestParameters()
    {
        var result = _context.Eval(@"
            function sum(...nums) {
                var total = 0;
                for (var i = 0; i < nums.length; i++) total += nums[i];
                return total;
            }
            sum(1, 2, 3, 4)
        ");
        Assert.Equal(10d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — destructuring
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_ArrayDestructuring()
    {
        var result = _context.Eval("var [a, b] = [10, 20]; a + b");
        Assert.Equal(30d, result.DoubleValue);
    }

    [Fact]
    public void Eval_ObjectDestructuring()
    {
        var result = _context.Eval("var {x, y} = {x: 5, y: 15}; x + y");
        Assert.Equal(20d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Eval — switch
    // ---------------------------------------------------------------

    [Fact]
    public void Eval_SwitchStatement()
    {
        var result = _context.Eval(@"
            var r;
            switch(2) {
                case 1: r = 'one'; break;
                case 2: r = 'two'; break;
                default: r = 'other';
            }
            r
        ");
        Assert.Equal("two", result.ToString());
    }
}
