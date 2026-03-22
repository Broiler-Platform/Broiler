using System;
using System.Text;
using System.Globalization;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Typed;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Core.Core.Generator;

namespace Broiler.JavaScript.Core.Core.Array;

public partial class JSArray
{
    [JSExport(IsConstructor = true, Length = 1)]
    public new static JSValue Constructor(in Arguments a)
    {
        var @this = a.This;
        var arg = a.Get1();
        var result = new JSArray();

        if (a.Length == 0)
            return new JSArray();

        if (a.Length == 1 && arg.IsNumber)
        {
            double val = arg.DoubleValue;
            if (double.IsNaN(val) || val < 0 || val > UInt32.MaxValue || Math.Floor(val) != val)
                throw JSContext.NewRangeError($"Invalid array length");
            return new JSArray((uint)arg.DoubleValue);
        }

        for (int i = 0; i < a.Length; i++)
        {
            var ele = a.GetAt(i);
            result.Add(ele);
        }

        return result;
    }
}
