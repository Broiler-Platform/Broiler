using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using System;
using Broiler.JavaScript.Core.Core.Boolean;

namespace Broiler.JavaScript.Core.Utils;

public class TypeConverter
{
    public static JSValue FromBasic(object value) => value switch
    {
        null => JSNull.Value,
        JSValue jv => jv,
        bool b1 => b1 ? JSBoolean.True : JSBoolean.False,
        uint ui1 => new JSNumber(ui1),
        int i1 => new JSNumber(i1),
        float f1 => new JSNumber(f1),
        double d1 => new JSNumber(d1),
        decimal d2 => new JSNumber((double)d2),
        string str => new JSString(str),
        _ => throw new NotSupportedException(),
    };
}
