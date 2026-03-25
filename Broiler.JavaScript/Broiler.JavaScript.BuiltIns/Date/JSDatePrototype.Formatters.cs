using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Globalization;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Date;

public partial class JSDate
{
    [JSExport("toDateString", Length = 0)]
    internal JSValue ToDateString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var date = value.ToLocalTime().ToString("ddd MMM dd yyyy", DateTimeFormatInfo.InvariantInfo);
        return new JSString(date);
    }

    [JSExport("toISOString", Length = 0)]
    internal JSValue ToISOString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var date = value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", DateTimeFormatInfo.InvariantInfo);
        return new JSString(date);
    }

    [JSExport("toJSON", Length = 1)]
    internal JSValue ToJSON(in Arguments a)
    {
        if (value == InvalidDate)
            return JSNull.Value;

        var date = value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", DateTimeFormatInfo.InvariantInfo);
        return new JSString(date);

    }

    [JSExport("toLocaleDateString", Length = 0)]
    internal JSValue ToLocaleDateString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var (locale, format) = a.Get2();
        string date = null;

        if (locale.IsNullOrUndefined)
        {
            date = value.ToString("D", DateTimeFormatInfo.CurrentInfo);
        }
        else
        {
            var culture = CultureInfo.GetCultureInfo(locale.ToString());

            if (format.IsNullOrUndefined)
            {
                date = value.ToString("D", culture);
            }
            else
            {
                if (format.IsString)
                {
                    date = value.ToString(format.ToString(), culture);
                }
                else
                {
                    if (format is JSObject obj)
                    {
                        if (IntlDateFormatter != null)
                            return IntlDateFormatter(culture, value, obj);
                    }
                }
            }
        }

        return new JSString(date);
    }

    [JSExport("toLocaleString", Length = 0)]
    internal JSValue ToLocaleString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var (locale, format) = a.Get2();
        string date = null;

        if (locale.IsNullOrUndefined)
        {
            date = value.ToString("F", DateTimeFormatInfo.CurrentInfo);
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
                    throw JSEngine.NewTypeError("Options not supported, use .Net String Formats");
                }
            }
        }

        return new JSString(date);
    }

    [JSExport("toLocaleTimeString", Length = 0)]
    internal JSValue ToLocaleTimeString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var (locale, format) = a.Get2();
        string date = null;

        if (locale.IsNullOrUndefined)
        {
            date = value.ToString("T", DateTimeFormatInfo.CurrentInfo);
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
                    throw JSEngine.NewTypeError("Options not supported, use .Net String Formats");
                }
            }
        }

        return new JSString(date);
    }

    [JSExport("toString", Length = 0)]
    internal new JSValue ToString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var date = value.ToString("ddd MMM dd yyyy HH:mm:ss ", DateTimeFormatInfo.InvariantInfo) + ToTimeZoneString();
        return new JSString(date);
    }

    [JSExport("toTimeString", Length = 0)]
    internal JSValue ToTimeString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        // DateTimeFormatInfo.CurrentInfo.LongTimePattern
        var date = value.ToString("HH:mm:ss ", DateTimeFormatInfo.InvariantInfo) + ToTimeZoneString();
        return new JSString(date);
    }

    [JSExport("toUTCString", Length = 0)]
    internal JSValue ToUTCString(in Arguments a)
    {
        if (value == InvalidDate)
            return new JSString("Invalid Date");

        var date = value.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", DateTimeFormatInfo.InvariantInfo);
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

    internal string ToTimeZoneString()
    {
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
