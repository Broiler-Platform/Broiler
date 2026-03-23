using System;

namespace Broiler.JavaScript.BuiltIns.Date;

/// <summary>
/// ECMAScript date math helpers per ECMA-262 § 21.4.1.
/// Supports proleptic Gregorian calendar (including year 0 and negative years).
/// </summary>
internal static class JSDateMath
{
    internal const long MsPerDay = 86_400_000L;

    /// <summary>Floor division that rounds toward negative infinity.</summary>
    private static long FloorDiv(long a, long b) => a >= 0 ? a / b : (a - b + 1) / b;

    /// <summary>Floor modulo that always returns a non-negative result.</summary>
    private static long FloorMod(long a, long b)
    {
        long r = a % b;
        return r < 0 ? r + b : r;
    }

    /// <summary>Number of days from epoch (Jan 1, 1970) to Jan 1 of the given year.</summary>
    internal static long DayFromYear(long y) => 365L * (y - 1970) + FloorDiv(y - 1969, 4) - FloorDiv(y - 1901, 100) + FloorDiv(y - 1601, 400);

    /// <summary>Millisecond timestamp for the start of the given year.</summary>
    internal static double TimeFromYear(long y) => MsPerDay * (double)DayFromYear(y);

    /// <summary>Whether the given year is a leap year in the proleptic Gregorian calendar.</summary>
    internal static bool IsLeapYear(long y)
    {
        long m4 = FloorMod(y, 4);
        long m100 = FloorMod(y, 100);
        long m400 = FloorMod(y, 400);

        return m4 == 0 && (m100 != 0 || m400 == 0);
    }

    /// <summary>Number of days in the given year.</summary>
    internal static int DaysInYear(long y) => IsLeapYear(y) ? 366 : 365;

    /// <summary>Day number from epoch.</summary>
    internal static long Day(double t) => (long)Math.Floor(t / MsPerDay);

    /// <summary>Milliseconds within the day.</summary>
    internal static long TimeWithinDay(double t) => FloorMod((long)t, MsPerDay);

    /// <summary>Find the year containing the given timestamp.</summary>
    internal static long YearFromTime(double t)
    {
        // Estimate using average year length (365.2425 days)
        long day = Day(t);
        long y = 1970 + (long)(day / 365.2425);

        // Adjust: DayFromYear(y) must be ≤ day, and DayFromYear(y+1) must be > day
        while (DayFromYear(y + 1) <= day) y++;
        while (DayFromYear(y) > day) y--;

        return y;
    }

    private static readonly int[] CumulativeDays = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
    private static readonly int[] CumulativeDaysLeap = { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335 };

    /// <summary>Month (0-11) from timestamp.</summary>
    internal static int MonthFromTime(double t)
    {
        long y = YearFromTime(t);
        long dayWithinYear = Day(t) - DayFromYear(y);
        var cumDays = IsLeapYear(y) ? CumulativeDaysLeap : CumulativeDays;

        for (int m = 11; m >= 0; m--)
        {
            if (dayWithinYear >= cumDays[m])
                return m;
        }

        return 0;
    }

    /// <summary>Day of month (1-31) from timestamp.</summary>
    internal static int DateFromTime(double t)
    {
        long y = YearFromTime(t);
        long dayWithinYear = Day(t) - DayFromYear(y);
        int m = MonthFromTime(t);
        var cumDays = IsLeapYear(y) ? CumulativeDaysLeap : CumulativeDays;

        return (int)(dayWithinYear - cumDays[m]) + 1;
    }

    /// <summary>Day of week (0=Sunday, 6=Saturday) from timestamp.</summary>
    internal static int WeekDay(double t) => (int)FloorMod(Day(t) + 4, 7);

    /// <summary>Hour (0-23) from timestamp.</summary>
    internal static int HourFromTime(double t) => (int)FloorMod((long)Math.Floor(t / 3_600_000), 24);

    /// <summary>Minute (0-59) from timestamp.</summary>
    internal static int MinFromTime(double t) => (int)FloorMod((long)Math.Floor(t / 60_000), 60);

    /// <summary>Second (0-59) from timestamp.</summary>
    internal static int SecFromTime(double t) => (int)FloorMod((long)Math.Floor(t / 1_000), 60);

    /// <summary>Millisecond (0-999) from timestamp.</summary>
    internal static int MsFromTime(double t) => (int)FloorMod((long)t, 1000);

    /// <summary>
    /// MakeDay per ECMA-262 § 21.4.1.13.
    /// Returns the day number for the given year/month/date combination.
    /// </summary>
    internal static double MakeDay(long year, long month, long date)
    {
        long yr = year + FloorDiv(month, 12);
        long mn = FloorMod(month, 12);

        long dayStart = DayFromYear(yr);
        var cumDays = IsLeapYear(yr) ? CumulativeDaysLeap : CumulativeDays;

        return dayStart + cumDays[(int)mn] + date - 1;
    }

    /// <summary>MakeDate per ECMA-262 § 21.4.1.14.</summary>
    internal static double MakeDate(double day, double time) => day * MsPerDay + time;

    /// <summary>MakeTime per ECMA-262 § 21.4.1.12.</summary>
    internal static double MakeTime(double hour, double min, double sec, double ms) => hour * 3_600_000 + min * 60_000 + sec * 1_000 + ms;

    /// <summary>
    /// UTC adjustment: convert local time to UTC.
    /// Uses the timezone offset for the date represented by <paramref name="t"/>
    /// when within .NET DateTimeOffset range; otherwise falls back to current offset.
    /// </summary>
    internal static double UTC(double t) => t - GetLocalOffsetMs(t);

    /// <summary>
    /// LocalTime adjustment: convert UTC to local time.
    /// Uses the timezone offset for the date represented by <paramref name="t"/>
    /// when within .NET DateTimeOffset range; otherwise falls back to current offset.
    /// </summary>
    internal static double LocalTime(double t) => t + GetLocalOffsetMs(t);

    /// <summary>
    /// Returns the local timezone offset in milliseconds for the given UTC timestamp.
    /// For dates outside .NET's supported range, falls back to the current offset.
    /// </summary>
    private static double GetLocalOffsetMs(double t)
    {
        long ms = (long)t;
        long minMs = DateTimeOffset.MinValue.ToUnixTimeMilliseconds();
        long maxMs = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

        if (ms >= minMs && ms <= maxMs)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            return TimeZoneInfo.Local.GetUtcOffset(dto).TotalMilliseconds;
        }

        // Fallback for dates outside .NET range (e.g., year 0 or negative years)
        return TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UtcNow).TotalMilliseconds;
    }

    /// <summary>
    /// TimeClip per ECMA-262 § 21.4.1.15.
    /// </summary>
    internal static double TimeClip(double time)
    {
        if (double.IsInfinity(time) || double.IsNaN(time))
            return double.NaN;

        if (Math.Abs(time) > 8.64e15)
            return double.NaN;

        return Math.Truncate(time);
    }
}
