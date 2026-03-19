using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for the Number built-in object.
/// Covers constructor, prototype methods, static properties, and static methods.
/// </summary>
public class NumberBuiltInTests : IDisposable
{
    private readonly JSContext _context;

    public NumberBuiltInTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // typeof / constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Number_TypeofLiteral_IsNumber()
    {
        var result = _context.Eval("typeof 42");
        Assert.Equal("number", result.ToString());
    }

    [Fact]
    public void Number_Constructor_ReturnsObjectWrapper()
    {
        var result = _context.Eval("typeof new Number(42)");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Number_FunctionCall_CoercesToNumber()
    {
        var result = _context.Eval("Number('123')");
        Assert.Equal(123d, result.DoubleValue);
    }

    [Fact]
    public void Number_FunctionCall_EmptyString_ReturnsZero()
    {
        var result = _context.Eval("Number('')");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Number_FunctionCall_True_ReturnsOne()
    {
        var result = _context.Eval("Number(true)");
        Assert.Equal(1d, result.DoubleValue);
    }

    [Fact]
    public void Number_FunctionCall_False_ReturnsZero()
    {
        var result = _context.Eval("Number(false)");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Number_FunctionCall_Null_ReturnsZero()
    {
        var result = _context.Eval("Number(null)");
        Assert.Equal(0d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Static properties
    // ---------------------------------------------------------------

    [Fact]
    public void Number_NaN_IsNaN()
    {
        var result = _context.Eval("Number.NaN");
        Assert.True(double.IsNaN(result.DoubleValue));
    }

    [Fact]
    public void Number_POSITIVE_INFINITY()
    {
        var result = _context.Eval("Number.POSITIVE_INFINITY");
        Assert.True(double.IsPositiveInfinity(result.DoubleValue));
    }

    [Fact]
    public void Number_NEGATIVE_INFINITY()
    {
        var result = _context.Eval("Number.NEGATIVE_INFINITY");
        Assert.True(double.IsNegativeInfinity(result.DoubleValue));
    }

    [Fact]
    public void Number_MAX_SAFE_INTEGER()
    {
        var result = _context.Eval("Number.MAX_SAFE_INTEGER");
        Assert.Equal(9007199254740991d, result.DoubleValue);
    }

    [Fact]
    public void Number_MIN_SAFE_INTEGER()
    {
        var result = _context.Eval("Number.MIN_SAFE_INTEGER");
        Assert.Equal(-9007199254740991d, result.DoubleValue);
    }

    [Fact]
    public void Number_EPSILON()
    {
        var result = _context.Eval("Number.EPSILON");
        // ES defines EPSILON as 2^-52 ≈ 2.2204460492503131e-16
        Assert.Equal(2.2204460492503131e-16, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Static methods
    // ---------------------------------------------------------------

    [Fact]
    public void Number_IsFinite_True()
    {
        var result = _context.Eval("Number.isFinite(42)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Number_IsFinite_InfinityFalse()
    {
        var result = _context.Eval("Number.isFinite(Infinity)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Number_IsFinite_NaNFalse()
    {
        var result = _context.Eval("Number.isFinite(NaN)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Number_IsNaN_True()
    {
        var result = _context.Eval("Number.isNaN(NaN)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Number_IsNaN_NumberFalse()
    {
        var result = _context.Eval("Number.isNaN(42)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Number_IsInteger_True()
    {
        var result = _context.Eval("Number.isInteger(42)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Number_IsInteger_FloatFalse()
    {
        var result = _context.Eval("Number.isInteger(42.5)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Number_IsSafeInteger_True()
    {
        var result = _context.Eval("Number.isSafeInteger(42)");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Number_IsSafeInteger_LargeNumberFalse()
    {
        var result = _context.Eval("Number.isSafeInteger(9007199254740992)");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Number_ParseFloat_ParsesDecimal()
    {
        var result = _context.Eval("Number.parseFloat('3.14')");
        Assert.Equal(3.14, result.DoubleValue, 10);
    }

    [Fact]
    public void Number_ParseInt_ParsesInteger()
    {
        var result = _context.Eval("Number.parseInt('42')");
        Assert.Equal(42d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Prototype methods
    // ---------------------------------------------------------------

    [Fact]
    public void Number_ToString_ReturnsStringRepresentation()
    {
        var result = _context.Eval("(42).toString()");
        Assert.Equal("42", result.ToString());
    }

    [Fact]
    public void Number_ToString_Radix16()
    {
        var result = _context.Eval("(255).toString(16)");
        Assert.Equal("ff", result.ToString());
    }

    [Fact]
    public void Number_ToString_Radix2()
    {
        var result = _context.Eval("(10).toString(2)");
        Assert.Equal("1010", result.ToString());
    }

    [Fact]
    public void Number_ToFixed_DefaultDigits()
    {
        var result = _context.Eval("(3.14159).toFixed(2)");
        Assert.Equal("3.14", result.ToString());
    }

    [Fact]
    public void Number_ValueOf_ReturnsNumber()
    {
        var result = _context.Eval("(42).valueOf()");
        Assert.Equal(42d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Arithmetic edge cases
    // ---------------------------------------------------------------

    [Fact]
    public void Number_DivisionByZero_PositiveInfinity()
    {
        var result = _context.Eval("1 / 0");
        Assert.True(double.IsPositiveInfinity(result.DoubleValue));
    }

    [Fact]
    public void Number_DivisionByZero_NegativeInfinity()
    {
        var result = _context.Eval("-1 / 0");
        Assert.True(double.IsNegativeInfinity(result.DoubleValue));
    }

    [Fact]
    public void Number_ZeroDivZero_NaN()
    {
        var result = _context.Eval("0 / 0");
        Assert.True(double.IsNaN(result.DoubleValue));
    }

    [Fact]
    public void Number_NaN_NotEqualToItself()
    {
        var result = _context.Eval("NaN === NaN");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Number_Infinity_PlusNumber()
    {
        var result = _context.Eval("Infinity + 1");
        Assert.True(double.IsPositiveInfinity(result.DoubleValue));
    }
}
