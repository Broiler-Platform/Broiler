using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using System;

namespace Broiler.JavaScript.BuiltIns.Date;

[JSFunctionGenerator("Date")]
public partial class JSDate: JSObject
{
    internal static readonly DateTimeOffset InvalidDate = DateTimeOffset.MinValue;
    internal static readonly JSDate invalidDate = new(DateTimeOffset.MinValue);

    internal static TimeSpan Local => TimeZoneInfo.Local.BaseUtcOffset;

    internal DateTimeOffset value;

    /// <summary>
    /// Raw ECMAScript time value (ms since epoch) for dates outside
    /// .NET DateTimeOffset range (e.g., year 0 or negative years).
    /// When NaN, the <see cref="value"/> field is authoritative.
    /// </summary>
    internal double rawTimeMs = double.NaN;

    public DateTimeOffset Value
    {
        get => value;
        set => this.value = value;
    }

    public DateTime DateTime => Value.DateTime;

    internal JSDate(JSObject prototype, DateTimeOffset time) : base(prototype) => value = time;

    public JSDate(DateTimeOffset time) : this() => value = time;

    public override string ToDetailString() => value.ToString();

    /// <summary>
    /// Returns the ECMAScript time value for this date (ms since epoch).
    /// Uses <see cref="rawTimeMs"/> when set (for dates outside .NET range),
    /// otherwise delegates to <see cref="JSDateStatic.ToJSDate"/>.
    /// </summary>
    internal double GetTimeMs()
    {
        if (!double.IsNaN(rawTimeMs))
            return rawTimeMs;

        return value.ToJSDate();
    }

    /// <summary>
    /// Returns true if this date is valid (not NaN / invalid).
    /// </summary>
    internal bool IsValidDate()
    {
        if (!double.IsNaN(rawTimeMs))
            return true;

        return value != InvalidDate;
    }

    public override bool ConvertTo(Type type, out object value)
    {
        if (type == typeof(DateTime))
        {
            value = this.value.LocalDateTime;
            return true;
        }

        if (type == typeof(DateTimeOffset))
        {
            value = this.value;
            return true;
        }

        if (type.IsAssignableFrom(typeof(JSDate)))
        {
            value = this;
            return true;
        }

        if (type == typeof(object))
        {
            value = this.value;
            return true;
        }

        return base.ConvertTo(type, out value);
    }
}
