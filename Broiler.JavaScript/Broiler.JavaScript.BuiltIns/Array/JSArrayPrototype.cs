using System;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Array;

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
            if (double.IsNaN(val) || val < 0 || val > uint.MaxValue || Math.Floor(val) != val)
                throw JSEngine.NewRangeError($"Invalid array length");
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
