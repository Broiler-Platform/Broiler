using System;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Array;

public partial class JSArray
{
    [JSPrototypeMethod]
    [JSExport("at", Length = 1)]
    public static JSValue At(in Arguments a)
    {
        var index = a[0];
        var @this = a.This;
        var length = a.Length;
        var i = index.IntegerValue;

        if (i < 0)
        {
            if (i < -length)
                return JSUndefined.Value;

            i += length;
        }

        if (i >= length)
            return JSUndefined.Value;

        return @this.GetOwnProperty((uint)i);
    }

    [JSPrototypeMethod]
    [JSExport("includes", Length = 1)]
    public static JSValue Includes(in Arguments a)
    {
        var @this = a.This;
        var first = a.Get1();

        var fromIndex = a[1]?.IntValue ?? 0;
        if (fromIndex < 0)
            fromIndex += @this.Length;

        bool isUndefined = first.IsUndefined;
        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            if (fromIndex > index)
                continue;

            if (hasValue)
            {
                if (item.SameValueZero(first))
                    return JSBoolean.True;
            }
            else
            {
                if (isUndefined)
                    return JSBoolean.True;
            }
        }

        return JSBoolean.False;
    }

    [JSPrototypeMethod]
    [JSExport("indexOf", Length = 1)]
    public static JSValue IndexOf(in Arguments a)
    {
        var @this = a.This;
        var first = a.Get1();
        var fromIndex = a[1]?.IntValue ?? 0;

        if (fromIndex < 0)
            fromIndex += @this.Length;

        var en = @this.GetElementEnumerator();

        while (en.MoveNext(out var hasValue, out var item, out var index))
        {
            if (fromIndex > index)
                continue;

            if (!hasValue)
                continue;

            if (first.StrictEquals(item))
                return new JSNumber(index);
        }

        return JSNumber.MinusOne;
    }

    [JSPrototypeMethod]
    [JSExport("lastIndexOf", Length = 1)]
    public static JSValue LastIndexOf(in Arguments a)
    {
        var @this = a.This;
        var first = a.Get1();
        var n = @this.Length;
        var fromIndex = a[1]?.IntValue ?? int.MaxValue;

        if (fromIndex < 0)
            fromIndex += @this.Length;

        if (n == 0)
            return JSNumber.MinusOne;

        for (int i = Math.Min(n - 1, fromIndex); i >= 0; i--)
        {
            if (!@this.TryGetElement((uint)i, out var item))
                continue;

            if (item.StrictEquals(first))
                return new JSNumber(i);
        }

        return JSNumber.MinusOne;
    }

}
