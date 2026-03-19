using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for the Math built-in object.
/// Covers properties (PI, E, etc.) and methods (abs, ceil, floor, max, min, etc.).
/// </summary>
public class MathBuiltInTests : IDisposable
{
    private readonly JSContext _context;

    public MathBuiltInTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------

    [Fact]
    public void Math_PI()
    {
        var result = _context.Eval("Math.PI");
        Assert.Equal(Math.PI, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_E()
    {
        var result = _context.Eval("Math.E");
        Assert.Equal(Math.E, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_LN2()
    {
        var result = _context.Eval("Math.LN2");
        Assert.Equal(Math.Log(2), result.DoubleValue, 10);
    }

    [Fact]
    public void Math_LN10()
    {
        var result = _context.Eval("Math.LN10");
        Assert.Equal(Math.Log(10), result.DoubleValue, 10);
    }

    [Fact]
    public void Math_SQRT2()
    {
        var result = _context.Eval("Math.SQRT2");
        Assert.Equal(Math.Sqrt(2), result.DoubleValue, 10);
    }

    // ---------------------------------------------------------------
    // Rounding
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Floor_PositiveDecimal()
    {
        var result = _context.Eval("Math.floor(4.7)");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Math_Floor_NegativeDecimal()
    {
        var result = _context.Eval("Math.floor(-4.1)");
        Assert.Equal(-5d, result.DoubleValue);
    }

    [Fact]
    public void Math_Ceil_PositiveDecimal()
    {
        var result = _context.Eval("Math.ceil(4.1)");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Math_Ceil_NegativeDecimal()
    {
        var result = _context.Eval("Math.ceil(-4.7)");
        Assert.Equal(-4d, result.DoubleValue);
    }

    [Fact]
    public void Math_Round_HalfUp()
    {
        var result = _context.Eval("Math.round(4.5)");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Math_Round_Down()
    {
        var result = _context.Eval("Math.round(4.4)");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Math_Trunc_PositiveDecimal()
    {
        var result = _context.Eval("Math.trunc(4.9)");
        Assert.Equal(4d, result.DoubleValue);
    }

    [Fact]
    public void Math_Trunc_NegativeDecimal()
    {
        var result = _context.Eval("Math.trunc(-4.9)");
        Assert.Equal(-4d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Min / Max
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Max_MultipleArgs()
    {
        var result = _context.Eval("Math.max(1, 5, 3)");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Math_Max_NegativeNumbers()
    {
        var result = _context.Eval("Math.max(-10, -5, -20)");
        Assert.Equal(-5d, result.DoubleValue);
    }

    [Fact]
    public void Math_Min_MultipleArgs()
    {
        var result = _context.Eval("Math.min(1, 5, 3)");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Math_Min_NegativeNumbers()
    {
        var result = _context.Eval("Math.min(-10, -5, -20)");
        Assert.Equal(-20d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Abs / Sign
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Abs_Positive()
    {
        var result = _context.Eval("Math.abs(42)");
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void Math_Abs_Negative()
    {
        var result = _context.Eval("Math.abs(-42)");
        Assert.Equal(42d, result.DoubleValue);
    }

    [Fact]
    public void Math_Sign_Positive()
    {
        var result = _context.Eval("Math.sign(100)");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Math_Sign_Negative()
    {
        var result = _context.Eval("Math.sign(-100)");
        Assert.Equal(-1d, result.DoubleValue);
    }

    [Fact]
    public void Math_Sign_Zero()
    {
        var result = _context.Eval("Math.sign(0)");
        Assert.Equal(0d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Power / Sqrt / Cbrt
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Pow_IntegerExponent()
    {
        var result = _context.Eval("Math.pow(2, 10)");
        Assert.Equal(1024d, result.DoubleValue);
    }

    [Fact]
    public void Math_Sqrt_PerfectSquare()
    {
        var result = _context.Eval("Math.sqrt(144)");
        Assert.Equal(12d, result.DoubleValue);
    }

    [Fact]
    public void Math_Cbrt_PerfectCube()
    {
        var result = _context.Eval("Math.cbrt(27)");
        Assert.Equal(3d, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Hypot_ThreeFourFive()
    {
        var result = _context.Eval("Math.hypot(3, 4)");
        Assert.Equal(5d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Log / Exp
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Log_One()
    {
        var result = _context.Eval("Math.log(1)");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Math_Log_E()
    {
        var result = _context.Eval("Math.log(Math.E)");
        Assert.Equal(1d, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Log2_Eight()
    {
        var result = _context.Eval("Math.log2(8)");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void Math_Log10_Thousand()
    {
        var result = _context.Eval("Math.log10(1000)");
        Assert.Equal(3d, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Exp_Zero()
    {
        var result = _context.Eval("Math.exp(0)");
        Assert.Equal(1d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Trigonometric
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Sin_Zero()
    {
        var result = _context.Eval("Math.sin(0)");
        Assert.Equal(0d, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Cos_Zero()
    {
        var result = _context.Eval("Math.cos(0)");
        Assert.Equal(1d, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Tan_Zero()
    {
        var result = _context.Eval("Math.tan(0)");
        Assert.Equal(0d, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Atan2_OneOne()
    {
        var result = _context.Eval("Math.atan2(1, 1)");
        Assert.Equal(Math.PI / 4, result.DoubleValue, 10);
    }

    // ---------------------------------------------------------------
    // Random
    // ---------------------------------------------------------------

    [Fact]
    public void Math_Random_BetweenZeroAndOne()
    {
        var result = _context.Eval("var r = Math.random(); r >= 0 && r < 1");
        Assert.True(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // typeof / not a function
    // ---------------------------------------------------------------

    [Fact]
    public void Math_IsObject()
    {
        var result = _context.Eval("typeof Math");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Math_NotConstructor()
    {
        Assert.ThrowsAny<Exception>(() => _context.Eval("new Math()"));
    }
}
