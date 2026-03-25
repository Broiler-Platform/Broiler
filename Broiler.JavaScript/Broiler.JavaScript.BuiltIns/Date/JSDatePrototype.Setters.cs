using System;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Date;

public partial class JSDate
{
    /// <summary>
    /// The setDate() method sets the day of the Date object relative to the beginning of the currently set month.
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    [JSExport("setDate", Length = 1)]
    internal JSValue SetDate(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var diffValue))
            return JSNumber.NaN;

        try
        {
            value = value.AddDays(-value.Day + diffValue);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setFullYear", Length = 3)]
    internal JSValue SetFullYear(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var year))
            return JSNumber.NaN;

        var date = value;
        var (_year, _month, _day) = a.Get3();

        var month = _month.IsUndefined ? date.Month - 1 : _month.IntValue;
        var day = (_day.IsUndefined ? date.Day : _day.IntValue);

        // For years that .NET DateTimeOffset can handle (1–9999), use the fast path.
        if (year >= 1 && year <= 9999)
        {
            rawTimeMs = double.NaN; // clear any raw override

            try
            {
                date = new DateTimeOffset((int)year, 1, 1, date.Hour, date.Minute, date.Second, date.Millisecond, value.Offset);
                date = date.AddDays(day - 1);
                date = date.AddMonths(month);
                value = date;
            }
            catch (ArgumentOutOfRangeException)
            {
                value = DateTimeOffset.MinValue;
            }

            return new JSNumber(value.ToJSDate());
        }

        // For year 0 or negative years, use ECMAScript date math directly.
        // This handles the proleptic Gregorian calendar correctly.
        double timeWithinDay = JSDateMath.MakeTime(date.Hour, date.Minute, date.Second, date.Millisecond);
        double dayValue = JSDateMath.MakeDay((long)year, month, day);
        double utcMs = JSDateMath.UTC(JSDateMath.MakeDate(dayValue, timeWithinDay));
        double result = JSDateMath.TimeClip(utcMs);

        rawTimeMs = result;
        value = DateTimeOffset.MinValue;

        return new JSNumber(result);
    }

    [JSExport("setHours", Length = 4)]
    internal JSValue SetHours(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var hours))
            return JSNumber.NaN;

        var date = value;
        var (_hours, _mins, _seconds, _millis) = a.Get4();

        var hrs = _hours.IsUndefined ? date.Hour : _hours.IntValue;
        var mins = _mins.IsUndefined ? date.Minute : _mins.IntValue;
        var seconds = _seconds.IsUndefined ? date.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? date.Millisecond : _millis.IntValue;

        try
        {
            date = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, 0, date.Offset);
            date = date.AddMilliseconds(millis);
            date = date.AddSeconds(seconds);
            date = date.AddMinutes(mins);
            date = date.AddHours(hrs);
            value = date;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setMilliseconds", Length = 1)]
    internal JSValue SetMilliseconds(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var _millis))
            return JSNumber.NaN;

        var date = value;

        try
        {
            date = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, 0, date.Offset);
            date = value.AddMilliseconds(_millis);
            value = date;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setMinutes", Length = 3)]
    internal JSValue SetMinutes(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var minutes))
            return JSNumber.NaN;

        var date = value;
        var (_mins, _seconds, _millis) = a.Get3();
        var mins = _mins.IsUndefined ? date.Minute : _mins.IntValue;
        var seconds = _seconds.IsUndefined ? date.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? date.Millisecond : _millis.IntValue;

        try
        {
            date = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, 0, 0, 0, date.Offset);
            date = date.AddMilliseconds(millis);
            date = date.AddSeconds(seconds);
            date = date.AddMinutes(mins);
            value = date;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setMonth", Length = 2)]
    internal JSValue SetMonth(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var mnth))
            return JSNumber.NaN;

        var date = value;
        var (_month, _days) = a.Get2();
        var month = _month.IsUndefined ? date.Month : _month.IntValue;
        var days = (_days.IsUndefined ? date.Day : _days.IntValue) - 1;

        try
        {
            date = new DateTimeOffset(date.Year, 1, 1, date.Hour, date.Minute, date.Second, date.Millisecond, date.Offset);
            date = date.AddDays(days);
            date = date.AddMonths(month);
            value = date;

        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setSeconds", Length = 2)]
    internal JSValue SetSeconds(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var secs))
            return JSNumber.NaN;

        var date = value;
        var (_seconds, _millis) = a.Get2();
        var seconds = _seconds.IsUndefined ? date.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? date.Millisecond : _millis.IntValue;

        try
        {
            date = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, 0, date.Offset);
            date = date.AddMilliseconds(millis);
            date = date.AddSeconds(seconds);
            value = date;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }


    [JSExport("setTime", Length = 1)]
    internal JSValue SetTime(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var _time))
            return JSNumber.NaN;

        try
        {
            value = DateTimeOffset.FromUnixTimeMilliseconds((long)_time).ToOffset(Local);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setUTCDate", Length = 1)]
    internal JSValue setUTCDate(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var _date))
            return JSNumber.NaN;

        try
        {
            var date = value;
            var offset = date.Offset;
            var utc = date.ToUniversalTime();

            utc = utc.AddDays(-utc.Day + _date);
            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setUTCFullYear", Length = 1)]
    internal JSValue setUTCFullYear(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var year))
            return JSNumber.NaN;

        if (year <= 0)
        {
            value = DateTimeOffset.MinValue;
            return JSNumber.NaN;
        }

        var date = value;
        var (_year, _month, _day) = a.Get3();

        var offset = date.Offset;
        var utc = date.ToUniversalTime();

        var month = _month.IsUndefined ? utc.Month - 1 : _month.IntValue;
        var day = (_day.IsUndefined ? utc.Day : _day.IntValue) - 1;

        try
        {
            utc = new DateTimeOffset((int)year, 1, 1, utc.Hour, utc.Minute, utc.Second, utc.Millisecond, utc.Offset);
            utc = utc.AddDays(day);
            utc = utc.AddMonths(month);
            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());

    }

    [JSExport("setUTCHours", Length = 4)]
    internal JSValue SetUTCHours(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var hours))
            return JSNumber.NaN;

        var date = value;
        var (_hours, _mins, _seconds, _millis) = a.Get4();

        var offset = date.Offset;
        var utc = date.ToUniversalTime();

        var hrs = _hours.IsUndefined ? utc.Hour : _hours.IntValue;
        var mins = _mins.IsUndefined ? utc.Minute : _mins.IntValue;
        var seconds = _seconds.IsUndefined ? utc.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? utc.Millisecond : _millis.IntValue;

        try
        {
            utc = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, 0, utc.Offset);
            utc = utc.AddMilliseconds(millis);
            utc = utc.AddSeconds(seconds);
            utc = utc.AddMinutes(mins);
            utc = utc.AddHours(hrs);
            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setUTCMilliseconds", Length = 1)]
    internal JSValue SetUTCMilliseconds(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var _millis))
            return JSNumber.NaN;

        var date = value;
        var offset = date.Offset;
        var utc = date.ToUniversalTime();

        try
        {
            utc = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, 0, utc.Offset);
            utc = utc.AddMilliseconds(_millis);

            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setUTCMinutes", Length = 3)]
    internal JSValue SetUTCMinutes(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var minutes))
            return JSNumber.NaN;

        var date = value;
        var offset = date.Offset;
        var utc = date.ToUniversalTime();

        var (_mins, _seconds, _millis) = a.Get3();

        var mins = _mins.IsUndefined ? utc.Minute : _mins.IntValue;
        var seconds = _seconds.IsUndefined ? utc.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? utc.Millisecond : _millis.IntValue;

        try
        {
            utc = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, 0, utc.Offset);
            utc = utc.AddMilliseconds(millis);
            utc = utc.AddSeconds(seconds);
            utc = utc.AddMinutes(mins);

            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setUTCMonth", Length = 2)]
    internal JSValue SetUTCMonth(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var mnth))
            return JSNumber.NaN;

        var date = value;
        var offset = date.Offset;
        var utc = date.ToUniversalTime();

        var (_month, _days) = a.Get2();

        var month = _month.IsUndefined ? utc.Month : _month.IntValue;
        var days = (_days.IsUndefined ? utc.Day : _days.IntValue) - 1;

        try
        {
            utc = new DateTimeOffset(utc.Year, 1, 1, utc.Hour, utc.Minute, utc.Second, utc.Millisecond, utc.Offset);
            utc = utc.AddDays(days);
            utc = utc.AddMonths(month);

            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setUTCSeconds", Length = 2)]
    internal JSValue SetUTCSeconds(in Arguments a)
    {
        if (!IsValid(a.Get1(), out var secs))
            return JSNumber.NaN;

        var date = value;
        var offset = date.Offset;
        var utc = date.ToUniversalTime();

        var (_seconds, _millis) = a.Get2();

        var seconds = _seconds.IsUndefined ? utc.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? utc.Millisecond : _millis.IntValue;

        try
        {
            utc = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, 0, utc.Offset);
            utc = utc.AddMilliseconds(millis);
            utc = utc.AddSeconds(seconds);

            value = utc.ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
        }

        return new JSNumber(value.ToJSDate());
    }
}
