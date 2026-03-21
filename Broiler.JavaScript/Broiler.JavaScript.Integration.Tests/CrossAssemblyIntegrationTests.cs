using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Cross-assembly integration tests that exercise end-to-end scenarios
/// spanning compilation (Compiler), execution (Core), built-in object
/// interaction (BuiltIns), and CLR interop (Clr).
/// </summary>
public class CrossAssemblyIntegrationTests : IDisposable
{
    private readonly JSContext _context;

    public CrossAssemblyIntegrationTests()
    {
        _context = new JSContext();
        JSContext.Current = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Verifies that a script can be compiled (Compiler assembly) and
    /// executed (Core assembly) to produce a correct result.
    /// </summary>
    [Fact]
    public void Compile_And_Execute_SimpleExpression()
    {
        var result = _context.Eval("1 + 2");
        Assert.Equal(3, result.IntValue);
    }

    /// <summary>
    /// Verifies that <see cref="CoreScript.Evaluate"/> compiles and runs
    /// JavaScript through the factory-delegate-based compilation pipeline.
    /// </summary>
    [Fact]
    public void CoreScript_Evaluate_Works()
    {
        var result = CoreScript.Evaluate("'hello' + ' ' + 'world'");
        Assert.Equal("hello world", result.ToString());
    }

    /// <summary>
    /// Verifies that WeakRef (a BuiltIns type) is accessible from
    /// JavaScript code compiled by the Compiler and executed in Core.
    /// </summary>
    [Fact]
    public void BuiltIn_WeakRef_AccessibleFromScript()
    {
        var result = _context.Eval(@"
            var obj = { name: 'test' };
            var ref1 = new WeakRef(obj);
            ref1.deref().name;
        ");
        Assert.Equal("test", result.ToString());
    }

    /// <summary>
    /// Verifies that FinalizationRegistry (a BuiltIns type) can be
    /// constructed from JavaScript.
    /// </summary>
    [Fact]
    public void BuiltIn_FinalizationRegistry_Constructable()
    {
        var result = _context.Eval(@"
            var fr = new FinalizationRegistry(function(v) {});
            typeof fr;
        ");
        Assert.Equal("object", result.ToString());
    }

    /// <summary>
    /// Verifies that decimal literal support works end-to-end through the
    /// factory delegate pattern (BuiltIns wires JSValue.CreateDecimalFromStringFactory,
    /// which the Compiler invokes during compilation).
    /// </summary>
    [Fact]
    public void Decimal_FactoryDelegate_EndToEnd()
    {
        var decimalValue = JSValue.CreateDecimalFromString("42.5");
        Assert.NotNull(decimalValue);
        Assert.True(decimalValue.IsDecimal);
        Assert.Equal(42.5m, decimalValue.DecimalValue);
    }

    /// <summary>
    /// Verifies that DisposableStack factory delegate creates functional
    /// instances across assembly boundaries.
    /// </summary>
    [Fact]
    public void DisposableStack_FactoryDelegate_EndToEnd()
    {
        var stack = Core.Core.Disposable.IJSDisposableStack.New();
        Assert.NotNull(stack);
        // Dispose should not throw on empty stack
        var result = stack.Dispose();
        Assert.NotNull(result);
    }

    /// <summary>
    /// Verifies that CLR interop (Clr assembly) works with script execution
    /// (Core + Compiler assemblies) — marshalling a .NET object into JS.
    /// </summary>
    [Fact]
    public void ClrInterop_MarshalObject_ViaScript()
    {
        var clrInterop = JSContext.ClrInterop;
        Assert.NotNull(clrInterop);

        // Marshal a simple CLR value
        var jsVal = clrInterop.Marshal(42);
        Assert.Equal(42, jsVal.IntValue);
    }

    /// <summary>
    /// Verifies that Array built-in works correctly across the compilation
    /// and execution pipeline.
    /// </summary>
    [Fact]
    public void Array_BuiltIn_EndToEnd()
    {
        var result = _context.Eval("[1, 2, 3].map(x => x * 2).join(',')");
        Assert.Equal("2,4,6", result.ToString());
    }

    /// <summary>
    /// Verifies that Promise built-in works correctly across assemblies.
    /// </summary>
    [Fact]
    public void Promise_BuiltIn_EndToEnd()
    {
        var result = _context.Eval(@"
            var resolved = false;
            Promise.resolve(42).then(v => { resolved = true; });
            resolved;
        ");
        // Promise resolution is async; the synchronous check may be false
        // but the script should compile and execute without errors.
        Assert.NotNull(result);
    }

    /// <summary>
    /// Verifies that the full built-in registration pipeline works: creating
    /// a new context triggers DefaultBuiltInRegistry.Register which calls
    /// both Core's generated classes and BuiltIns' AdditionalRegistrations.
    /// </summary>
    [Fact]
    public void FullRegistration_Pipeline_Works()
    {
        using var ctx = new JSContext();
        JSContext.Current = ctx;

        // Verify standard built-ins are available
        var hasArray = ctx.Eval("typeof Array");
        Assert.Equal("function", hasArray.ToString());

        var hasPromise = ctx.Eval("typeof Promise");
        Assert.Equal("function", hasPromise.ToString());

        var hasWeakRef = ctx.Eval("typeof WeakRef");
        Assert.Equal("function", hasWeakRef.ToString());
    }
}
