using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for the Date built-in object.
/// Covers constructor forms, prototype methods, and static methods.
/// </summary>
public class DateBuiltInTests : IDisposable
{
    private readonly JSContext _context;

    public DateBuiltInTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------

    [Fact]
    public void Date_Constructor_NoArgs_ReturnsCurrentTime()
    {
        var result = _context.Eval("typeof new Date()");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Date_Constructor_Milliseconds()
    {
        var result = _context.Eval("new Date(0).getTime()");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Date_Constructor_YearMonth()
    {
        var result = _context.Eval("new Date(2020, 0, 1).getFullYear()");
        Assert.Equal(2020d, result.DoubleValue);
    }

    [Fact]
    public void Date_Constructor_FullComponents()
    {
        var result = _context.Eval("new Date(2023, 5, 15, 10, 30, 45).getMonth()");
        Assert.Equal(5d, result.DoubleValue); // June (0-indexed)
    }

    // ---------------------------------------------------------------
    // Getters
    // ---------------------------------------------------------------

    [Fact]
    public void Date_GetFullYear()
    {
        var result = _context.Eval("new Date(2023, 0, 1).getFullYear()");
        Assert.Equal(2023d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetMonth_ZeroBased()
    {
        var result = _context.Eval("new Date(2023, 11, 25).getMonth()");
        Assert.Equal(11d, result.DoubleValue); // December
    }

    [Fact]
    public void Date_GetDate_DayOfMonth()
    {
        var result = _context.Eval("new Date(2023, 0, 15).getDate()");
        Assert.Equal(15d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetDay_DayOfWeek()
    {
        // Jan 1, 2023 was a Sunday (0)
        var result = _context.Eval("new Date(2023, 0, 1).getDay()");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetHours()
    {
        var result = _context.Eval("new Date(2023, 0, 1, 14, 30).getHours()");
        Assert.Equal(14d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetMinutes()
    {
        var result = _context.Eval("new Date(2023, 0, 1, 14, 30).getMinutes()");
        Assert.Equal(30d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetSeconds()
    {
        var result = _context.Eval("new Date(2023, 0, 1, 14, 30, 45).getSeconds()");
        Assert.Equal(45d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetMilliseconds()
    {
        var result = _context.Eval("new Date(2023, 0, 1, 0, 0, 0, 500).getMilliseconds()");
        Assert.Equal(500d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetTime_ReturnsEpochMillis()
    {
        // Epoch 0 is Jan 1 1970 00:00:00 UTC
        var result = _context.Eval("new Date(0).getTime()");
        Assert.Equal(0d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Setters
    // ---------------------------------------------------------------

    [Fact]
    public void Date_SetFullYear()
    {
        var result = _context.Eval("var d = new Date(2020, 0, 1); d.setFullYear(2025); d.getFullYear()");
        Assert.Equal(2025d, result.DoubleValue);
    }

    [Fact]
    public void Date_SetMonth()
    {
        var result = _context.Eval("var d = new Date(2023, 0, 1); d.setMonth(6); d.getMonth()");
        Assert.Equal(6d, result.DoubleValue);
    }

    [Fact]
    public void Date_SetDate()
    {
        var result = _context.Eval("var d = new Date(2023, 0, 1); d.setDate(20); d.getDate()");
        Assert.Equal(20d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Static methods
    // ---------------------------------------------------------------

    [Fact]
    public void Date_Now_ReturnsNumber()
    {
        var result = _context.Eval("typeof Date.now()");
        Assert.Equal("number", result.ToString());
    }

    [Fact]
    public void Date_Now_IsPositive()
    {
        var result = _context.Eval("Date.now() > 0");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Date_Parse_ReturnsNumber()
    {
        var result = _context.Eval("typeof Date.parse('2023-01-01')");
        Assert.Equal("number", result.ToString());
    }

    // ---------------------------------------------------------------
    // toString / toISOString / toJSON
    // ---------------------------------------------------------------

    [Fact]
    public void Date_ToString_ReturnsString()
    {
        var result = _context.Eval("typeof new Date(2023, 0, 1).toString()");
        Assert.Equal("string", result.ToString());
    }

    [Fact]
    public void Date_ToISOString_Format()
    {
        // new Date(Date.UTC(2023, 0, 1)) ensures UTC
        var result = _context.Eval("new Date(Date.UTC(2023, 0, 1)).toISOString()");
        Assert.Contains("2023-01-01", result.ToString());
    }

    [Fact]
    public void Date_ValueOf_ReturnsEpochMillis()
    {
        var result = _context.Eval("new Date(0).valueOf()");
        Assert.Equal(0d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // UTC getters
    // ---------------------------------------------------------------

    [Fact]
    public void Date_GetUTCFullYear()
    {
        var result = _context.Eval("new Date(Date.UTC(2023, 0, 1)).getUTCFullYear()");
        Assert.Equal(2023d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetUTCMonth()
    {
        var result = _context.Eval("new Date(Date.UTC(2023, 5, 15)).getUTCMonth()");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void Date_GetUTCDate()
    {
        var result = _context.Eval("new Date(Date.UTC(2023, 5, 15)).getUTCDate()");
        Assert.Equal(15d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Date.UTC
    // ---------------------------------------------------------------

    [Fact]
    public void Date_UTC_ReturnsMilliseconds()
    {
        var result = _context.Eval("Date.UTC(1970, 0, 1)");
        Assert.Equal(0d, result.DoubleValue);
    }

    [Fact]
    public void Date_UTC_SpecificDate()
    {
        // Date.UTC(2023, 0, 1) is Jan 1 2023 00:00:00 UTC in milliseconds
        var result = _context.Eval("Date.UTC(2023, 0, 1)");
        Assert.True(result.DoubleValue > 0);
    }

    // ---------------------------------------------------------------
    // Invalid date
    // ---------------------------------------------------------------

    [Fact]
    public void Date_InvalidString_GetTime_IsNaN()
    {
        var result = _context.Eval("new Date('invalid').getTime()");
        Assert.True(double.IsNaN(result.DoubleValue));
    }

    // ---------------------------------------------------------------
    // Comparison via getTime
    // ---------------------------------------------------------------

    [Fact]
    public void Date_Comparison_ByGetTime()
    {
        var result = _context.Eval(@"
            var d1 = new Date(2023, 0, 1);
            var d2 = new Date(2024, 0, 1);
            d1.getTime() < d2.getTime()
        ");
        Assert.True(result.BooleanValue);
    }
}
