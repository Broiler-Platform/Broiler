using Broiler.JavaScript.Runtime;
using System;
using System.Globalization;

namespace Broiler.JavaScript.BuiltIns.Date;

public partial class JSDate
{
    static long MinTime = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
    static long MaxTime = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>
    /// Factory delegate for formatting a date using Intl.DateTimeFormat.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// Signature: (CultureInfo culture, DateTimeOffset value, JSObject options) → JSValue
    /// </summary>
    internal static Func<CultureInfo, DateTimeOffset, JSObject, JSValue> IntlDateFormatter { get; set; }

    [JSExport(Length = 7)]
    JSDate(in Arguments a)
    {
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

        if (a.Length == 1)
        {
            if (dateString.IsNumber)
            {
                var ticks = dateString.BigIntValue;
                ticks = Math.Max(MinTime, ticks);
                ticks = Math.Min(MaxTime, ticks);
                date = DateTimeOffset.FromUnixTimeMilliseconds(ticks);

                if (ticks == MinTime || ticks == MaxTime)
                {
                    value = date;
                    return;
                }

                value = date.ToOffset(Local);
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

        var (year, month, day, hours, minutes, seconds, millis) = a.Get7Int();

        day = day - 1;
        try
        {
            year = year >= 0 && year < 100 ? year + 1900 : year;
            date = new DateTimeOffset(year, 1, 1, 0, 0, 0, 0, Local);
            date = date.AddMilliseconds(millis);
            date = date.AddSeconds(seconds);
            date = date.AddMinutes(minutes);
            date = date.AddHours(hours);
            date = date.AddDays(day);
            date = date.AddMonths(month);
            value = date;

            return;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = DateTimeOffset.MinValue;
            return;
        }
    }

}
