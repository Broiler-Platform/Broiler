using System;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Date;

namespace Broiler.JavaScript.Core.Core.Clr;

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
            return JSNull.Value;

        if (value is JSValue jsv)
            return jsv;

        var t = Type.GetTypeCode(value.GetType());

        return t switch
        {
            TypeCode.Boolean => (bool)value ? JSBoolean.True : JSBoolean.False,
            TypeCode.Byte => new JSNumber((byte)value),
            TypeCode.Char => new JSString(value.ToString()),
            TypeCode.DateTime => new JSDate((DateTime)value),
            TypeCode.DBNull => JSNull.Value,
            TypeCode.Decimal => new JSNumber((double)(decimal)value),
            TypeCode.Double => new JSNumber((double)value),
            TypeCode.Int16 => new JSNumber((short)value),
            TypeCode.Int32 => new JSNumber((int)value),
            TypeCode.Int64 => new JSNumber((long)value),
            TypeCode.SByte => new JSNumber((sbyte)value),
            TypeCode.Single => new JSNumber((float)value),
            TypeCode.String => new JSString((string)value),
            TypeCode.UInt16 => new JSNumber((ushort)value),
            TypeCode.UInt32 => new JSNumber((uint)value),
            TypeCode.UInt64 => new JSNumber((long)value),
            _ => JSUndefined.Value
        };
    }

    public JSValue GetClrType(Type type) => JSUndefined.Value;

    public bool TryUnwrapClrObject(JSValue value, out object clrObject)
    {
        clrObject = null;
        return false;
    }
}
