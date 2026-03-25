using Broiler.JavaScript.ExpressionCompiler;
using System;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Objects;

[JSClassGenerator("Math"), JSInternalObject]
public partial class JSMath : JSObject
{
    static Random randomGenertor = new();

    internal static double RandomNumber() => randomGenertor.NextDouble();


    [JSExportSameName]
    public readonly static double E = Math.E;

    [JSExportSameName]
    public readonly static double LN10 = Math.Log(10);

    [JSExportSameName]
    public readonly static double LN2 = Math.Log(2);

    [JSExportSameName]
    public readonly static double LOG10E = Math.Log10(E);

    [JSExportSameName]
    public readonly static double LOG2E = Math.Log(E);

    [JSExportSameName]
    public readonly static double PI = Math.PI;

    [JSExportSameName]
    public readonly static double SQRT1_2 = Math.Sqrt(0.5);

    [JSExportSameName]
    public readonly static double SQRT2 = Math.Sqrt(2);

    [JSExport]
    public static JSValue Random(in Arguments a) => new JSNumber(randomGenertor.NextDouble());

    [JSExport]
    public static JSValue Round(in Arguments args)
    {
        var first = args.Get1();
        if (first.IsUndefined)
            return JSNumber.NaN;

        if (first.IsNull)
            return JSNumber.Zero;

        if (first.IsDecimal)
        {
            var dv = first.DecimalValue;
            return JSValue.CreateDecimal(Math.Floor(dv + 0.5m));
        }

        var number = first.DoubleValue;
        if (number > 0.0)
            return new JSNumber(Math.Floor(number + 0.5));

        if (number >= -0.5)
        {
            // BitConverter is used to distinguish positive and negative zero.
            if (BitConverter.DoubleToInt64Bits(number) == 0L)
                return JSNumber.Zero;
            return new JSNumber(-0.0D);
        }

        return new JSNumber(Math.Floor(number + 0.5));
    }

    /// <summary>
    /// We do not want to recreate new objects for standard known constants. 
    /// Hence, we need to check and return already existing constants.
    /// 
    /// </summary>
    /// <param name="t"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    [JSExport]
    public static JSValue Floor(in Arguments args)
    {
        var first = args.Get1();
        if (first.IsDecimal)
            return JSValue.CreateDecimal(Math.Floor(first.DecimalValue));

        var d = first.DoubleValue;
        var r = new JSNumber(Math.Floor(d));
        return r;
    }

    [JSExport]
    public static JSValue Acos(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Acos(d));

        return r;
    }

    [JSExport]
    public static JSValue Abs(in Arguments args)
    {
        var first = args.Get1();
        if (first.IsDecimal)
            return JSValue.CreateDecimal(Math.Abs(first.DecimalValue));

        var d = first.DoubleValue;
        var r = new JSNumber(Math.Abs(d));
        return r;
    }

    [JSExport]
    public static JSValue Acosh(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Log(d + Math.Sqrt((d * d) - 1.0)));

        return r;
    }

    [JSExport]
    public static JSValue Asin(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Asin(d));

        return r;
    }

    [JSExport]
    public static JSValue Asinh(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Log(d + Math.Sqrt(d * d + 1.0)));
        return r;
    }

    [JSExport]
    public static JSValue Atan(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Atan(d));
        return r;
    }

    [JSExport]
    public static JSValue Atan2(in Arguments args)
    {
        var (first, second) = args.Get2();
        var d1 = first.DoubleValue;
        var d2 = second.DoubleValue;

        if (double.IsInfinity(d1) || double.IsInfinity(d2))
        {
            if (double.IsPositiveInfinity(d1) && double.IsPositiveInfinity(d2))
                return new JSNumber(Math.PI / 4.0);

            if (double.IsPositiveInfinity(d1) && double.IsNegativeInfinity(d2))
                return new JSNumber(3.0 * Math.PI / 4.0);

            if (double.IsNegativeInfinity(d1) && double.IsPositiveInfinity(d2))
                return new JSNumber(-Math.PI / 4.0);

            if (double.IsNegativeInfinity(d1) && double.IsNegativeInfinity(d2))
                return new JSNumber(-3.0 * Math.PI / 4.0);
        }

        var r = new JSNumber(Math.Atan2(d1, d2));
        return r;
    }

    [JSExport]
    public static JSValue Atanh(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Log((1.0 + d) / (1.0 - d)) / 2.0);

        return r;
    }

    [JSExport]
    public static JSValue Cbrt(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = Math.Pow(Math.Abs(d), 1.0 / 3.0);

        return new JSNumber(d < 0 ? -r : r);
    }

    [JSExport]
    public static JSValue Ceil(in Arguments args)
    {
        var first = args.Get1();
        if (first.IsDecimal)
            return JSValue.CreateDecimal(Math.Ceiling(first.DecimalValue));

        var d = first.DoubleValue;
        var r = new JSNumber(Math.Ceiling(d));
        return r;
    }

    private static readonly int[] clz32Table = [
        32, 31,  0, 16,  0, 30,  3,  0, 15,  0,  0,  0, 29, 10,  2,  0,
         0,  0, 12, 14, 21,  0, 19,  0,  0, 28,  0, 25,  0,  9,  1,  0,
        17,  0,  4,  0,  0,  0, 11,  0, 13, 22, 20,  0, 26,  0,  0, 18,
         5,  0,  0, 23,  0, 27,  0,  6,  0, 24,  7,  0,  8,  0,  0,  0
    ];


    /// <summary>
    /// we have Int value, so we might want to replace DoubleValue with Intvalue, 
    /// But since the implementation is not complete, we have continued with Doublevalue
    /// https://github.com/paulbartrum/jurassic/blob/0522bcb42b29f87bdf65ae74b9a450179c1d168d/Jurassic/Library/MathObject.cs#L475
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    [JSExport]
    public static JSValue Clz32(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var x = ((uint)d) >> 0;

        x |= x >> 1;       // Propagate leftmost
        x |= x >> 2;       // 1-bit to the right.
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        x *= 0x06EB14F9;     // Multiplier is 7*255**3.

        var r = clz32Table[x >> 26];
        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Cos(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Cos(d));

        return r;
    }

    [JSExport]
    public static JSValue Cosh(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Cosh(d));

        return r;
    }

    [JSExport]
    public static JSValue Exp(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Exp(d));
        return r;
    }

    [JSExport]
    public static JSValue Expm1(in Arguments args)
    {
        var first = args.Get1();
        double r;
        var d = first.DoubleValue;

        if (Math.Abs(d) < 0.01)
        {
            // For small numbers, use a taylor series approximation.
            r = d * (1.0 + d * (1.0 / 2.0 + d * (1.0 / 6.0 + d *
                (1.0 / 24.0 + d * (1.0 / 120.0 + d * (1.0 / 720.0 + d * (1.0 / 5040.0)))))));

            return new JSNumber(r);
        }

        // Otherwise just use the normal exp function.
        r = Math.Exp(d) - 1.0;
        return new JSNumber(r);

    }

    [JSExport]
    public static JSValue Fround(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = (double)(float)d;

        return new JSNumber(r);
    }

    /// <summary>
    /// ES2025 §2.8 — Math.f16round(x)
    /// Rounds a number to the nearest IEEE 754 half-precision (16-bit) float.
    /// </summary>
    [JSExport("f16round")]
    public static JSValue F16round(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = (double)(Half)d;

        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Hypot(in Arguments args)
    {
        int length = args.Length;

        if (length == 0)
            return JSNumber.Zero;

        if (length == 1)
            return new JSNumber(Math.Abs(args.Get1().DoubleValue));

        var (first, second) = args.Get2();
        double d1 = first.DoubleValue;
        double d2 = second.DoubleValue;

        if (length == 2)
            return new JSNumber(Hypot(d1, d2));

        double result = Hypot(d1, d2);
        for (int i = 2; i < length; i++)
        {
            double val = args.GetAt(i).DoubleValue;
            result = Hypot(result, val);
        }

        return new JSNumber(result);
    }

    public static double Hypot(double number1, double number2)
    {
        double abs1 = Math.Abs(number1);
        double abs2 = Math.Abs(number2);
        double min = Math.Min(abs1, abs2);
        double max = Math.Max(abs1, abs2);
        double u = min / max;

        if (min == 0)
            return max;

        return max * Math.Sqrt(1 + u * u);
    }

    [JSExport]
    public static JSValue Imul(in Arguments args)
    {
        var (first, second) = args.Get2();
        var d1 = first.IntValue;
        var d2 = second.IntValue;
        var r = d1 * d2;

        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Log(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = Math.Log(d);

        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Log10(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = Math.Log10(d);

        return new JSNumber(r);
    }


    [JSExport]
    public static JSValue Log1p(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        double r;

        if (Math.Abs(d) < 0.01)
        {
            // For small numbers, use a taylor series approximation.
            r = d * (1.0 + d * (-1.0 / 2.0 + d * (1.0 / 3.0 + d *
                (-1.0 / 4.0 + d * (1.0 / 5.0 + d * (-1.0 / 6.0 + d * (1.0 / 7.0)))))));
            return new JSNumber(r);
        }

        // Otherwise just use the normal log function.
        r = Math.Log(1.0 + d);
        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Log2(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = Math.Log(d) / LN2;

        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Max(in Arguments args)
    {
        int length = args.Length;
        double result = double.NegativeInfinity;

        for (int i = 0; i < length; i++)
        {
            double val = args.GetAt(i).DoubleValue;

            if (val > result || double.IsNaN(val))
                result = val;
        }

        return new JSNumber(result);
    }

    [JSExport]
    public static JSValue Min(in Arguments args)
    {
        int length = args.Length;
        double result = double.PositiveInfinity;

        for (int i = 0; i < length; i++)
        {
            double val = args.GetAt(i).DoubleValue;

            if (val < result || double.IsNaN(val))
                result = val;
        }

        return new JSNumber(result);
    }

    [JSExport]
    public static JSValue Pow(in Arguments args)
    {
        var (first, second) = args.Get2();
        var @base = first.DoubleValue;
        var exponent = second.DoubleValue;

        if ((@base == 1.0 || @base == -1) && double.IsInfinity(exponent))
            return JSNumber.NaN;

        if (double.IsNaN(@base) && exponent == 0.0)
            return JSNumber.One;

        var r = Math.Pow(@base, exponent);
        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Sign(in Arguments args)
    {
        var first = args.Get1();

        if (first.IsDecimal)
            return JSValue.CreateDecimal(Math.Sign(first.DecimalValue));

        var d = first.DoubleValue;

        if (double.IsNaN(d))
            return JSNumber.NaN;

        if (d == -0.0)
            return JSNumber.NegativeZero;

        var r = Math.Sign(d);
        return new JSNumber(r);
    }

    [JSExport]
    public static JSValue Sin(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Sin(d));

        return r;
    }

    [JSExport]
    public static JSValue Sinh(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Sinh(d));

        return r;
    }

    [JSExport]
    public static JSValue Sqrt(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Sqrt(d));

        return r;
    }

    [JSExport]
    public static JSValue Tan(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Tan(d));

        return r;
    }

    [JSExport]
    public static JSValue Tanh(in Arguments args)
    {
        var first = args.Get1();
        var d = first.DoubleValue;
        var r = new JSNumber(Math.Tanh(d));

        return r;
    }

    [JSExport]
    public static JSValue Trunc(in Arguments args)
    {
        var first = args.Get1();
        if (first.IsDecimal)
            return JSValue.CreateDecimal(Math.Truncate(first.DecimalValue));

        var d = first.DoubleValue;
        var r = new JSNumber(Math.Truncate(d));
        return r;
    }

    /// <summary>
    /// ES2026 §4.2 — Math.sumPrecise(iterable)
    /// Returns the sum of values from an iterable using Neumaier compensated
    /// summation for improved floating-point precision.
    /// </summary>
    [JSExport("sumPrecise")]
    public static JSValue SumPrecise(in Arguments args)
    {
        var iterable = args.Get1();
        if (iterable.IsNullOrUndefined)
            throw JSEngine.NewTypeError("Math.sumPrecise requires an iterable argument");

        double sum = 0.0;
        double compensation = 0.0;
        bool hasPositiveInfinity = false;
        bool hasNegativeInfinity = false;

        var en = iterable.GetElementEnumerator();
        while (en.MoveNext(out var hasValue, out var item, out var _))
        {
            if (!hasValue)
                continue;

            var d = item.DoubleValue;

            if (double.IsNaN(d))
                return JSNumber.NaN;

            if (double.IsPositiveInfinity(d))
            {
                if (hasNegativeInfinity)
                    return JSNumber.NaN;

                hasPositiveInfinity = true;
                continue;
            }

            if (double.IsNegativeInfinity(d))
            {
                if (hasPositiveInfinity)
                    return JSNumber.NaN;

                hasNegativeInfinity = true;
                continue;
            }

            // Neumaier compensated summation
            var t = sum + d;
            if (Math.Abs(sum) >= Math.Abs(d))
                compensation += (sum - t) + d;
            else
                compensation += (d - t) + sum;
            sum = t;
        }

        if (hasPositiveInfinity)
            return JSNumber.PositiveInfinity;

        if (hasNegativeInfinity)
            return JSNumber.NegativeInfinity;

        return new JSNumber(sum + compensation);
    }
}
