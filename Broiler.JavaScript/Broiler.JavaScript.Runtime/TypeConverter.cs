using System;

namespace Broiler.JavaScript.Runtime;

public class TypeConverter
{
    public static JSValue FromBasic(object value) => value switch
    {
        null => JSValue.NullValue,
        JSValue jv => jv,
        bool b1 => b1 ? JSValue.BooleanTrue : JSValue.BooleanFalse,
        uint ui1 => JSValue.CreateNumber(ui1),
        int i1 => JSValue.CreateNumber(i1),
        float f1 => JSValue.CreateNumber(f1),
        double d1 => JSValue.CreateNumber(d1),
        decimal d2 => JSValue.CreateNumber((double)d2),
        string str => JSValue.CreateString(str),
        _ => throw new NotSupportedException(),
    };
}
