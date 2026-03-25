using System;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Minimal <see cref="IClrInterop"/> implementation used when the full
/// <c>Broiler.JavaScript.Clr</c> assembly is not loaded.  Handles
/// primitives and <see cref="JSValue"/> pass-through; complex CLR object
/// wrapping is not supported.
/// </summary>
internal sealed class FallbackClrInterop : IClrInterop
{
    public static readonly FallbackClrInterop Instance = new();

    public JSValue Marshal(object value)
    {
        if (value == null)
            return JSValue.NullValue;

        if (value is JSValue jsv)
            return jsv;

        var t = Type.GetTypeCode(value.GetType());

        return t switch
        {
            TypeCode.Boolean => (bool)value ? JSValue.BooleanTrue : JSValue.BooleanFalse,
            TypeCode.Byte => JSValue.CreateNumber((byte)value),
            TypeCode.Char => JSValue.CreateString(value.ToString()),
            TypeCode.DateTime => JSValue.CreateDate(new DateTimeOffset((DateTime)value)),
            TypeCode.DBNull => JSValue.NullValue,
            TypeCode.Decimal => JSValue.CreateNumber((double)(decimal)value),
            TypeCode.Double => JSValue.CreateNumber((double)value),
            TypeCode.Int16 => JSValue.CreateNumber((short)value),
            TypeCode.Int32 => JSValue.CreateNumber((int)value),
            TypeCode.Int64 => JSValue.CreateNumber((long)value),
            TypeCode.SByte => JSValue.CreateNumber((sbyte)value),
            TypeCode.Single => JSValue.CreateNumber((float)value),
            TypeCode.String => JSValue.CreateString((string)value),
            TypeCode.UInt16 => JSValue.CreateNumber((ushort)value),
            TypeCode.UInt32 => JSValue.CreateNumber((uint)value),
            TypeCode.UInt64 => JSValue.CreateNumber((long)value),
            _ => JSValue.UndefinedValue
        };
    }

    public JSValue GetClrType(Type type) => JSValue.UndefinedValue;

    public bool TryUnwrapClrObject(JSValue value, out object clrObject)
    {
        clrObject = null;
        return false;
    }
}
