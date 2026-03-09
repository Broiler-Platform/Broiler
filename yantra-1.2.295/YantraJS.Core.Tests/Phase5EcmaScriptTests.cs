using System.Threading;

namespace YantraJS.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for Phase 5 — Hardening and Known Limitations:
/// L1 (Date year 0 handling), L4 (Number precision), L8 (Promise without SynchronizationContext).
/// </summary>
public class Phase5EcmaScriptTests : IDisposable
{
    private readonly JSContext _context;
    private readonly SynchronizationContext _previousSyncCtx;

    public Phase5EcmaScriptTests()
    {
        _previousSyncCtx = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        _context = new JSContext(SynchronizationContext.Current);
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
        SynchronizationContext.SetSynchronizationContext(_previousSyncCtx);
    }

    // ===============================================================
    // §L1  Date year 0 handling
    // ===============================================================

    [Fact]
    public void Date_SetFullYear_Zero_ReturnsValidDate()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(0);
            isNaN(d.getTime()) ? 'invalid' : 'valid';
        ");
        Assert.Equal("valid", result.ToString());
    }

    [Fact]
    public void Date_SetFullYear_Zero_GetFullYear_ReturnsZero()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(0);
            d.getFullYear();
        ");
        Assert.Equal(0.0, result.DoubleValue);
    }

    [Fact]
    public void Date_SetFullYear_Zero_GetTime_ReturnsFiniteNumber()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(0);
            isFinite(d.getTime());
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Date_SetFullYear_Zero_GetMonth_ReturnsZero()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(0);
            d.getMonth();
        ");
        Assert.Equal(0.0, result.DoubleValue);
    }

    [Fact]
    public void Date_SetFullYear_Zero_GetDate_ReturnsOne()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(0);
            d.getDate();
        ");
        Assert.Equal(1.0, result.DoubleValue);
    }

    [Fact]
    public void Date_SetFullYear_NegativeYear()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(-1);
            d.getFullYear();
        ");
        Assert.Equal(-1.0, result.DoubleValue);
    }

    [Fact]
    public void Date_SetFullYear_PositiveYear_StillWorks()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 5, 15);
            d.setFullYear(2025);
            d.getFullYear();
        ");
        Assert.Equal(2025.0, result.DoubleValue);
    }

    [Fact]
    public void Date_SetFullYear_Zero_WithMonthAndDay()
    {
        var result = _context.Eval(@"
            var d = new Date(2000, 0, 1);
            d.setFullYear(0, 5, 15);
            d.getFullYear() + '/' + d.getMonth() + '/' + d.getDate();
        ");
        Assert.Equal("0/5/15", result.ToString());
    }

    // ===============================================================
    // §L4  Number precision (ECMAScript-compliant toString)
    // ===============================================================

    [Fact]
    public void Number_ToString_Zero()
    {
        var result = _context.Eval("(0).toString()");
        Assert.Equal("0", result.ToString());
    }

    [Fact]
    public void Number_ToString_NegativeZero()
    {
        var result = _context.Eval("(-0).toString()");
        Assert.Equal("0", result.ToString());
    }

    [Fact]
    public void Number_ToString_NaN()
    {
        var result = _context.Eval("NaN.toString()");
        Assert.Equal("NaN", result.ToString());
    }

    [Fact]
    public void Number_ToString_Infinity()
    {
        var result = _context.Eval("Infinity.toString()");
        Assert.Equal("Infinity", result.ToString());
    }

    [Fact]
    public void Number_ToString_NegativeInfinity()
    {
        var result = _context.Eval("(-Infinity).toString()");
        Assert.Equal("-Infinity", result.ToString());
    }

    [Fact]
    public void Number_ToString_Integer()
    {
        var result = _context.Eval("(42).toString()");
        Assert.Equal("42", result.ToString());
    }

    [Fact]
    public void Number_ToString_Decimal()
    {
        var result = _context.Eval("(12.34).toString()");
        Assert.Equal("12.34", result.ToString());
    }

    [Fact]
    public void Number_ToString_SmallDecimal()
    {
        // 0.000001 = 1e-6, which should use decimal (n = -5, within -5 ≤ n ≤ 0)
        var result = _context.Eval("(0.000001).toString()");
        Assert.Equal("0.000001", result.ToString());
    }

    [Fact]
    public void Number_ToString_VerySmallDecimal_ScientificNotation()
    {
        // 0.0000001 = 1e-7, which should use scientific notation (n = -6, outside -5..0)
        var result = _context.Eval("(0.0000001).toString()");
        Assert.Equal("1e-7", result.ToString());
    }

    [Fact]
    public void Number_ToString_5e_Minus_6()
    {
        // 5e-6 = 0.000005 should use decimal notation
        var result = _context.Eval("(5e-6).toString()");
        Assert.Equal("0.000005", result.ToString());
    }

    [Fact]
    public void Number_ToString_LargeInteger_NoScientific()
    {
        // 1e20 should NOT use scientific notation (n ≤ 21)
        var result = _context.Eval("(1e20).toString()");
        Assert.Equal("100000000000000000000", result.ToString());
    }

    [Fact]
    public void Number_ToString_LargeInteger_Scientific()
    {
        // 1e21 should use scientific notation (n > 21)
        var result = _context.Eval("(1e21).toString()");
        Assert.Equal("1e+21", result.ToString());
    }

    [Fact]
    public void Number_ToString_Half()
    {
        var result = _context.Eval("(0.5).toString()");
        Assert.Equal("0.5", result.ToString());
    }

    [Fact]
    public void Number_ToString_Precision_0_1_Plus_0_2()
    {
        // Classic floating-point precision test
        var result = _context.Eval("(0.1 + 0.2).toString()");
        Assert.Equal("0.30000000000000004", result.ToString());
    }

    [Fact]
    public void Number_ToString_Negative()
    {
        var result = _context.Eval("(-42).toString()");
        Assert.Equal("-42", result.ToString());
    }

    [Fact]
    public void Number_ToString_NegativeDecimal()
    {
        var result = _context.Eval("(-3.14).toString()");
        Assert.Equal("-3.14", result.ToString());
    }

    [Fact]
    public void Number_ToString_100()
    {
        var result = _context.Eval("(100).toString()");
        Assert.Equal("100", result.ToString());
    }

    [Fact]
    public void Number_ToString_1_5()
    {
        var result = _context.Eval("(1.5).toString()");
        Assert.Equal("1.5", result.ToString());
    }

    [Fact]
    public void Number_ImplicitConversion_MatchesToString()
    {
        // String concatenation should use the same formatting as toString
        var result = _context.Eval("'' + 1e20");
        Assert.Equal("100000000000000000000", result.ToString());
    }

    [Fact]
    public void Number_ImplicitConversion_5e_Minus_6()
    {
        var result = _context.Eval("'' + 5e-6");
        Assert.Equal("0.000005", result.ToString());
    }

    // ===============================================================
    // §L8  Promise without SynchronizationContext
    // ===============================================================

    [Fact]
    public void Promise_WithoutSyncContext_DoesNotThrow()
    {
        // Create a context without SynchronizationContext
        var prevCtx = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            using var ctx = new JSContext(null);
            JSContext.CurrentContext = ctx;
            // This should NOT throw TypeError
            var result = ctx.Eval(@"
                var resolved = false;
                var p = new Promise(function(resolve) { resolve(42); });
                p.then(function(v) { resolved = v; });
                resolved;
            ");
            // The value should be resolved (since we have synchronous fallback)
            Assert.Equal(42.0, result.DoubleValue);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prevCtx);
            // Restore the original context for other tests
            JSContext.CurrentContext = _context;
        }
    }

    [Fact]
    public void Promise_WithSyncContext_StillWorks()
    {
        // Promises with SynchronizationContext should continue to work.
        // .then() is asynchronous with SynchronizationContext, so we verify
        // the promise creation and resolution do not throw.
        var result = _context.Eval(@"
            var p = new Promise(function(resolve) { resolve(10); });
            typeof p.then === 'function';
        ");
        Assert.True(result.BooleanValue);
    }
}
