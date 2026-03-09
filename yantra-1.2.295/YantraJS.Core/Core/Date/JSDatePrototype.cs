using System;
using System.Globalization;
using YantraJS.Core.Clr;
using YantraJS.Core.Core.Intl;
using YantraJS.Utils;

namespace YantraJS.Core;

public partial class JSDate
{

    static long MinTime = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
    static long MaxTime = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    [JSExport( Length = 7 )]
    JSDate(in Arguments a) {
        DateTimeOffset date;
        if (a.Length == 0)
        {
            value = DateTimeOffset.Now;
            return;
        }
        var dateString = a.Get1();
        if (dateString.IsNumber && double.IsNaN(dateString.DoubleValue))
        {
            value = DateTimeOffset.MinValue;
            return;
        }
        if (a.Length == 1) {
            if (dateString.IsNumber) {
                var ticks = dateString.BigIntValue;
                ticks = Math.Max(MinTime, ticks);
                ticks = Math.Min(MaxTime, ticks);
                date = DateTimeOffset.FromUnixTimeMilliseconds(ticks);
                if (ticks == MinTime || ticks == MaxTime)
                {
                    value = date;
                    return;
                }
                value = date.ToOffset(JSDate.Local);
                return;
            }
            date = DateParser.Parse(dateString.ToString());
            if (date == DateTimeOffset.MinValue)
            {
                value = date;
                return;
            }
            value = date.ToLocalTime();
            return;
        }
        var (year, month, day, hours, minutes, seconds, millis ) = a.Get7Int();

        day = day - 1;
        try
        {
            year = year >= 0 && year < 100 ? year + 1900 : year;
            date = new DateTimeOffset(year, 1, 1, 0, 0, 0, 0, JSDate.Local);
            date = date.AddMilliseconds(millis);
            date = date.AddSeconds(seconds);
            date = date.AddMinutes(minutes);
            date = date.AddHours(hours);
            date = date.AddDays(day);
            date = date.AddMonths(month);
            value = date;
            return;
        }
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
            return;
        }

    }



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

    /// <summary>
    /// The setDate() method sets the day of the Date object relative to the beginning of the currently set month.
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    [JSExport("setDate", Length = 1)]
    internal JSValue SetDate(in Arguments a)
    {
        
        if(!IsValid(a.Get1(),out var diffValue))
            return JSNumber.NaN;

        try
        {
            value = value.AddDays(-value.Day + diffValue);
        }
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
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


    //[JSExport("getYear", Length = 0)]
    //internal JSValue GetYear(in Arguments a)
    //{
    //    
    //    if (this.value == DateTimeOffset.MinValue)
    //        return JSNumber.NaN;
    //    var result = this.value.Year;
    //    return new JSNumber(result);
    //}

    [JSExport("setFullYear", Length = 3)]
    internal JSValue SetFullYear(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var year))

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
                date = new DateTimeOffset((int)year, 1, 1, 
                    date.Hour, date.Minute, date.Second, 
                    date.Millisecond, value.Offset);
                date = date.AddDays(day - 1);
                date = date.AddMonths(month);
                value = date;
            } catch (ArgumentOutOfRangeException)
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
        
        if (!IsValid( a.Get1(), out var hours))

            return JSNumber.NaN;

        var date = value;

        var (_hours, _mins, _seconds, _millis) = a.Get4();

        var hrs = _hours.IsUndefined ? date.Hour : _hours.IntValue;
        var mins = _mins.IsUndefined ? date.Minute : _mins.IntValue;
        var seconds = _seconds.IsUndefined ? date.Second : _seconds.IntValue;
        var millis = _millis.IsUndefined ? date.Millisecond : _millis.IntValue;
        try
        {
            date = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, 0, 
                date.Offset);
            date = date.AddMilliseconds(millis);
            date = date.AddSeconds(seconds);
            date = date.AddMinutes(mins);
            date = date.AddHours(hrs);
            value = date;
        }
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }



    [JSExport("setMilliseconds", Length = 1)]
    internal JSValue SetMilliseconds(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var _millis))

            return JSNumber.NaN;

        var date = value;
        try
        {
            date = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, 
                                            date.Minute, date.Second, 0, date.Offset);
            date = value.AddMilliseconds(_millis);
            value = date;
        }
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setMinutes", Length =3)]
    internal JSValue SetMinutes(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var minutes))

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
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }

    [JSExport("setMonth", Length = 2)]
    internal JSValue SetMonth(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var mnth))

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
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }


    [JSExport("setSeconds", Length = 2)]
    internal JSValue SetSeconds(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var secs))

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
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }


    [JSExport("setTime", Length = 1)]
    internal JSValue SetTime(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var _time))

            return JSNumber.NaN;
        try
        {
            value = DateTimeOffset.FromUnixTimeMilliseconds((long)_time).ToOffset(JSDate.Local);
        }
        catch (ArgumentOutOfRangeException) {
            value = DateTimeOffset.MinValue;
        }
        return new JSNumber(value.ToJSDate());
    }


    [JSExport("setUTCDate", Length = 1)]
    internal JSValue setUTCDate(in Arguments a)
    {
        
        if (!IsValid( a.Get1(), out var _date))

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
        
        if (!IsValid( a.Get1(), out var year))

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

        var month = _month.IsUndefined ? utc.Month - 1: _month.IntValue;
        var day = (_day.IsUndefined ? utc.Day : _day.IntValue) - 1;

        try
        {

            utc = new DateTimeOffset((int)year, 1, 1,
                utc.Hour, utc.Minute, utc.Second,
                utc.Millisecond, utc.Offset);
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
        
        if (!IsValid( a.Get1(), out var hours))

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
            utc = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, 0,
                utc.Offset);
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
        
        if (!IsValid( a.Get1(), out var _millis))

            return JSNumber.NaN;

        var date = value;
        var offset = date.Offset;
        var utc = date.ToUniversalTime();
       
        try
        {
            utc = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour,
                                            utc.Minute, utc.Second, 0, utc.Offset);
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
        
        if (!IsValid( a.Get1(), out var minutes))

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
        
        if (!IsValid( a.Get1(), out var mnth))

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
        
        if (!IsValid( a.Get1(), out var secs))

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


    [JSExport("toDateString", Length = 0)]
    internal JSValue ToDateString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var date =  value.ToLocalTime().ToString("ddd MMM dd yyyy", System.Globalization.DateTimeFormatInfo.InvariantInfo);

        return new JSString(date);
        
    }

    [JSExport("toISOString", Length = 0)]
    internal JSValue ToISOString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var date = value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.DateTimeFormatInfo.InvariantInfo);

        return new JSString(date);

    }


    [JSExport("toJSON", Length = 1)]
    internal JSValue ToJSON(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return JSNull.Value;
        var date = value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.DateTimeFormatInfo.InvariantInfo);

        return new JSString(date);

    }


    [JSExport("toLocaleDateString", Length = 0)]
    internal JSValue ToLocaleDateString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var (locale, format) = a.Get2();
        string date = null;
        if (locale.IsNullOrUndefined)
        {
            date = value.ToString("D", System.Globalization.DateTimeFormatInfo.CurrentInfo);
        }
        else {
            var culture = CultureInfo.GetCultureInfo(locale.ToString());
            if (format.IsNullOrUndefined) {
                date = value.ToString("D", culture);
            } else
            {
                if (format.IsString)
                {
                    date = value.ToString(format.ToString(), culture);
                }
                else {
                    if (format is JSObject obj)
                    {
                        return JSIntlDateTimeFormat.Get(culture).Format(value, obj);
                    }
                }
            }
        }
        return new JSString(date);

    }


    [JSExport("toLocaleString", Length = 0)]
    internal JSValue ToLocaleString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var (locale, format) = a.Get2();
        string date = null;
        if (locale.IsNullOrUndefined)
        {
            date = value.ToString("F", System.Globalization.DateTimeFormatInfo.CurrentInfo);
        }
        else
        {
            var culture = CultureInfo.GetCultureInfo(locale.ToString());
            if (format.IsNullOrUndefined)
            {
                date = value.ToString("F", culture);
            }
            else
            {
                if (format.IsString)
                {
                    date = value.ToString(format.ToString(), culture);
                }
                else
                {
                    throw JSContext.Current.NewTypeError("Options not supported, use .Net String Formats");
                }
            }
        }
        return new JSString(date);

    }



    [JSExport("toLocaleTimeString", Length = 0)]
    internal JSValue ToLocaleTimeString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var (locale, format) = a.Get2();
        string date = null;
        if (locale.IsNullOrUndefined)
        {
            date = value.ToString("T", System.Globalization.DateTimeFormatInfo.CurrentInfo);
        }
        else
        {
            var culture = CultureInfo.GetCultureInfo(locale.ToString());
            if (format.IsNullOrUndefined)
            {
                date = value.ToString("T", culture);
            }
            else
            {
                if (format.IsString)
                {
                    date = value.ToString(format.ToString(), culture);
                }
                else
                {
                    throw JSContext.Current.NewTypeError("Options not supported, use .Net String Formats");
                }
            }
        }
        return new JSString(date);

    }



    [JSExport("toString", Length = 0)]
    internal new JSValue ToString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var date = value.
                   ToString("ddd MMM dd yyyy HH:mm:ss ", System.Globalization.DateTimeFormatInfo.InvariantInfo) +
                   ToTimeZoneString();

        return new JSString(date);

    }



    [JSExport("toTimeString", Length = 0)]
    internal JSValue ToTimeString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        // DateTimeFormatInfo.CurrentInfo.LongTimePattern
        var date = value.
                   ToString("HH:mm:ss ", System.Globalization.DateTimeFormatInfo.InvariantInfo) +
                   ToTimeZoneString();

        return new JSString(date);

    }




    [JSExport("toUTCString", Length = 0)]
    internal JSValue ToUTCString(in Arguments a)
    {
        
        if (value == JSDate.InvalidDate)
            return new JSString("Invalid Date");
        var date = value.ToUniversalTime().
                   ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", 
                   System.Globalization.DateTimeFormatInfo.InvariantInfo);

        return new JSString(date);

    }







    [JSExport("valueOf", Length = 0)]
    internal new JSValue ValueOf(in Arguments a)
    {
        
        if (value == DateTimeOffset.MinValue)
            return JSNumber.NaN;
        var result = value.ToJSDate();
        return new JSNumber(result);
    }


    internal string ToTimeZoneString() {
        var timeZone = TimeZoneInfo.Local;
        // Compute the time zone offset in hours-minutes.
        int offsetInMinutes = (int)timeZone.GetUtcOffset(value).TotalMinutes;
        int hhmm = offsetInMinutes / 60 * 100 + offsetInMinutes % 60;

        // Get the time zone name.
        string zoneName;
        if (timeZone.IsDaylightSavingTime(value))
            zoneName = timeZone.DaylightName;
        else
            zoneName = timeZone.StandardName;

        if (hhmm < 0)
            return $"GMT{hhmm:d4} ({zoneName})";
        else
            return $"GMT+{hhmm:d4} ({zoneName})";
    }

}
