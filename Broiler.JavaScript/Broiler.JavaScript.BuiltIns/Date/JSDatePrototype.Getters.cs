using System;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Date;

public partial class JSDate
{
    [JSExport("getDate", Length = 0)]
    internal JSValue GetDate(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.DateFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Day;
        return new JSNumber(result);
    }

    /// <summary>
    /// If invalid date, return false
    /// If diff is undefined or NaN, return false
    /// else return true
    /// </summary>
    /// <param name="this"></param>
    /// <param name="diff"></param>
    /// <param name="diffValue"></param>
    /// <returns></returns>
    internal bool IsValid(JSValue diff, out double diffValue)
    {
        diffValue = 0;

        if (value == DateTimeOffset.MinValue)
            return false;

        if (diff.IsUndefined)
        {
            value = DateTimeOffset.MinValue;
            return false;
        }

        diffValue = diff.DoubleValue;

        if (double.IsNaN(diffValue))
        {
            value = DateTimeOffset.MinValue;
            return false;
        }

        return true;
    }

    [JSExport("getDay", Length = 0)]
    internal JSValue GetDay(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.WeekDay(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.DayOfWeek;
        return new JSNumber((double)result);
    }

    [JSExport("getFullYear", Length = 0)]
    internal JSValue GetFullYear(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.YearFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Year;
        return new JSNumber(result);
    }

    [JSExport("getHours", Length = 0)]
    internal JSValue GetHours(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.HourFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Hour;
        return new JSNumber(result);
    }

    [JSExport("getMilliseconds", Length = 0)]
    internal JSValue GetMilliSeconds(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.MsFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Millisecond;
        return new JSNumber(result);
    }

    [JSExport("getMinutes", Length = 0)]
    internal JSValue GetMinutes(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.MinFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Minute;
        return new JSNumber(result);
    }

    [JSExport("getMonth", Length = 0)]
    internal JSValue GetMonth(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.MonthFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Month - 1;
        return new JSNumber(result);
    }

    [JSExport("getSeconds", Length = 0)]
    internal JSValue GetSeconds(in Arguments a)
    {
        if (!double.IsNaN(rawTimeMs))
        {
            double localMs = JSDateMath.LocalTime(rawTimeMs);
            return new JSNumber(JSDateMath.SecFromTime(localMs));
        }

        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.Second;
        return new JSNumber(result);
    }

    [JSExport("getTime", Length = 0)]
    internal JSValue GetTime(in Arguments a)
    {
        if (!IsValidDate())
            return JSNumber.NaN;

        return new JSNumber(GetTimeMs());
    }

    [JSExport("getTimezoneOffset", Length = 0)]
    internal JSValue GetTimezoneOffset(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = -(int)TimeZoneInfo.Local.GetUtcOffset(Value).TotalMinutes;
        return new JSNumber(result);
    }

    [JSExport("getUTCDate", Length = 0)]
    internal JSValue GetUTCDate(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Day;
        return new JSNumber(result);
    }

    [JSExport("getUTCDay", Length = 0)]
    internal JSValue GetUTCDay(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().DayOfWeek;
        return new JSNumber((double)result);
    }

    [JSExport("getUTCFullYear", Length = 0)]
    internal JSValue GetUTCFullYear(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Year;
        return new JSNumber(result);
    }

    [JSExport("getUTCHours", Length = 0)]
    internal JSValue GetUTCHours(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Hour;
        return new JSNumber(result);
    }

    [JSExport("getUTCMilliseconds", Length = 0)]
    internal JSValue GetUTCMilliseconds(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Millisecond;
        return new JSNumber(result);
    }

    [JSExport("getUTCMinutes", Length = 0)]
    internal JSValue GetUTCMinutes(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Minute;
        return new JSNumber(result);
    }

    [JSExport("getUTCMonth", Length = 0)]
    internal JSValue GetUTCMonth(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Month - 1;
        return new JSNumber(result);
    }

    [JSExport("getUTCSeconds", Length = 0)]
    internal JSValue GetUTCSeconds(in Arguments a)
    {
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;

        var result = value.ToUniversalTime().Second;
        return new JSNumber(result);
    }
}
