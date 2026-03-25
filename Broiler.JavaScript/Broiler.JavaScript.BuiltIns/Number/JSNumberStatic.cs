using Broiler.JavaScript.Extensions;
using Broiler.JavaScript.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.BuiltIns.Number;

public partial class JSNumber
{
    [JSExport("isFinite")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue IsFinite(in Arguments a)
    {
        if (a.Get1() is JSNumber n)
        {
            if (!double.IsNaN(n.value) && n.value > double.NegativeInfinity && n.value < double.PositiveInfinity)
                return JSValue.BooleanTrue;
        }

        return JSValue.BooleanFalse;
    }

    [JSExport("isInteger")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue IsInteger(in Arguments a)
    {
        if (a.Get1() is JSNumber n)
        {
            var v = n.value;

            if (!double.IsInfinity(v))
            {
                if (Math.Floor(v) == v)
                    return JSValue.BooleanTrue;
            }
        }

        return JSValue.BooleanFalse;
    }

    [JSExport("isNaN")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue IsNaN(in Arguments a)
    {
        var first = a.GetAt(0);
        if (first.IsNumber)
            return double.IsNaN(first.DoubleValue) ? JSValue.BooleanTrue : JSValue.BooleanFalse;

        return JSValue.BooleanFalse;
    }

    [JSExport("isSafeInteger")]
    public static JSValue IsSafeInteger(in Arguments a)
    {
        if (a.Get1() is JSNumber n)
        {
            var v = n.value;
            if (!double.IsInfinity(v))
            {
                if (Math.Floor(v) == v && v >= MinSafeInteger && v <= MaxSafeInteger)
                    return JSValue.BooleanTrue;
            }
        }

        return JSValue.BooleanFalse;
    }

    [JSExport("parseFloat")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue ParseFloat(in Arguments a)
    {
        var result = NumberParser.ParseFloat(a.Get1().ToString());
        return new JSNumber(result);
    }

    [JSExport("parseInt")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue ParseInt(in Arguments a)
    {
        var nan = NaN;

        if (a.Length <= 0)
            return nan;

        var p = a.Get1();
        if (p.IsNumber)
            return p;

        if (p.IsNull || p.IsUndefined)
            return nan;

        var text = p.JSTrim();
        if (text.Length == 0)
            return nan;

        var radix = 0;
        if (a.Length > 1)
        {
            var (_, a1) = a.Get2();
            if (a1.IsNull || a1.IsUndefined)
            {
                radix = 0;
            }
            else
            {
                var n = a1.DoubleValue;
                if (!double.IsNaN(n))
                {
                    radix = a1.IntValue;
                    if (radix < 0 || radix == 1 || radix > 36)
                        return nan;
                }
            }
        }

        var d = NumberParser.ParseInt(text.Trim(), radix, false);
        return new JSNumber(d);
    }
}
