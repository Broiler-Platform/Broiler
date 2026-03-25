using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.ExpressionCompiler;
using System;
using System.Globalization;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Number;

[JSBaseClass("Object")]
[JSFunctionGenerator("Number")]
public sealed partial class JSNumber : JSPrimitive
{
    internal readonly double value;

    /// <summary>
    /// Gets the underlying numeric value of this JSNumber instance.
    /// </summary>
    public double NumberValue => value;

    private static readonly long positiveZeroBits = BitConverter.DoubleToInt64Bits(0.0);

    /// <summary>
    /// Determines if the given number is positive zero.
    /// </summary>
    /// <param name="value"> The value to test. </param>
    /// <returns> <c>true</c> if the value is positive zero; <c>false</c> otherwise. </returns>
    public static bool IsPositiveZero(double value) => BitConverter.DoubleToInt64Bits(value) == positiveZeroBits;

    private static readonly long negativeZeroBits = BitConverter.DoubleToInt64Bits(-0.0);

    /// <summary>
    /// Determines if the given number is negative zero.
    /// </summary>
    /// <param name="value"> The value to test. </param>
    /// <returns> <c>true</c> if the value is negative zero; <c>false</c> otherwise. </returns>
    public static bool IsNegativeZero(double value) => BitConverter.DoubleToInt64Bits(value) == negativeZeroBits;

    [JSExport("NaN")]
    public static JSNumber NaN = new(double.NaN);

    public static JSNumber MinusOne = new(-1);
    public static JSNumber Zero = new(0d);
    public static JSNumber NegativeZero = new(-0d);
    public static JSNumber One = new(1d);
    public static JSNumber Two = new(2d);

    [JSExport("POSITIVE_INFINITY")]
    public static JSNumber PositiveInfinity = new(double.PositiveInfinity);

    [JSExport("NEGATIVE_INFINITY")]
    public static JSNumber NegativeInfinity = new(double.NegativeInfinity);


    [JSExport("EPSILON")]
    public static readonly double Epsilon = 2.2204460492503130808472633361816E-16;

    [JSExport("MAX_SAFE_INTEGER")]
    public static readonly double MaxSafeInteger = 9007199254740991d;

    [JSExport("MAX_VALUE")]
    public static readonly double MaxValue = double.MaxValue;

    [JSExport("MIN_SAFE_INTEGER")]
    public static readonly double MinSafeInteger = -9007199254740991d;

    //Javascript considers double.Epsilon as MIN_VALUE and not .Net double.MinValue
    [JSExport("MIN_VALUE")]
    public static readonly double MinValue = double.Epsilon;

    public override bool IsNumber => true;

    public override JSValue TypeOf() => JSConstants.Number;

    protected override JSValue GetPrototype() => ((JSEngine.Current as JSObject)?[Names.Number] as JSFunction).prototype;

    internal override PropertyKey ToKey(bool create = false)
    {
        var n = value;

        if (double.IsNaN(n))
            return KeyStrings.NaN;

        if (n == 0)
            return 0;

        if (n > 0 && ((uint)n) == n)
            return (uint)n;

        if (!create)
        {
            if (KeyStrings.TryGet(n.ToString(), out var k))
                return k;

            return KeyStrings.undefined;
        }

        return KeyStrings.GetOrCreate(n.ToString());
    }

    public JSNumber(double value) : base() => this.value = value;

    public override double DoubleValue => value;

    public override bool BooleanValue => !double.IsNaN(value) && value != 0;

    public override long BigIntValue => (long)value;

    public override bool ConvertTo(Type type, out object value)
    {
        if (type == typeof(double))
        {
            value = this.value;
            return true;
        }

        if (type == typeof(float))
        {
            value = this.value;
            return true;
        }

        if (type == typeof(int))
        {
            value = (int)this.value;
            return true;
        }

        if (type == typeof(long))
        {
            value = (long)this.value;
            return true;
        }

        if (type == typeof(ulong))
        {
            value = (ulong)this.value;
            return true;
        }

        if (type == typeof(bool))
        {
            value = this.value != 0;
            return true;
        }

        if (type == typeof(short))
        {
            value = (short)this.value;
            return true;
        }

        if (type == typeof(uint))
        {
            value = (uint)this.value;
            return true;
        }

        if (type == typeof(ushort))
        {
            value = (ushort)this.value;
            return true;
        }

        if (type == typeof(byte))
        {
            value = (byte)this.value;
            return true;
        }

        if (type == typeof(sbyte))
        {
            value = (sbyte)this.value;
            return true;
        }

        if (type == typeof(object))
        {
            value = this.value;
            return true;
        }

        if (type.IsAssignableFrom(typeof(JSNumber)))
        {
            value = this;
            return true;
        }

        value = null;
        return false;
    }

    public override string ToString() => ToECMAString(value);

    /// <summary>
    /// ECMAScript-compliant Number::toString(x) per ECMA-262 § 6.1.6.1.20.
    /// Produces the shortest decimal representation that round-trips through
    /// IEEE 754 double-precision, formatted using ECMAScript rules for when
    /// to use decimal vs scientific notation.
    /// </summary>
    internal static string ToECMAString(double value)
    {
        if (double.IsNaN(value))
            return "NaN";

        if (double.IsPositiveInfinity(value))
            return "Infinity";

        if (double.IsNegativeInfinity(value))
            return "-Infinity";

        if (value == 0.0)
            return "0";

        if (value < 0)
            return "-" + ToECMAString(-value);

        // Get the round-trip representation using InvariantCulture
        // to ensure '.' as decimal separator.
        string repr = value.ToString("R", CultureInfo.InvariantCulture);

        // Parse repr into significand digits and base-10 exponent
        // such that value = significand × 10^exp  (significand is an integer string).
        int eIdx = repr.IndexOf('E');
        string intMantissa;
        int exp = 0;

        if (eIdx >= 0)
        {
            intMantissa = repr.Substring(0, eIdx);
            exp = int.Parse(repr.AsSpan(eIdx + 1), CultureInfo.InvariantCulture);
        }
        else
        {
            intMantissa = repr;
        }

        int dotIdx = intMantissa.IndexOf('.');
        if (dotIdx >= 0)
        {
            int fracLen = intMantissa.Length - dotIdx - 1;
            intMantissa = string.Concat(intMantissa.AsSpan(0, dotIdx), intMantissa.AsSpan(dotIdx + 1));
            exp -= fracLen;
        }

        // Remove leading zeros (e.g., from "0.001" → "0001")
        intMantissa = intMantissa.TrimStart('0');
        if (intMantissa.Length == 0) return "0";

        // Remove trailing zeros and adjust exponent
        // e.g., "420" exp=0 → "42" exp=1
        int origLen = intMantissa.Length;
        intMantissa = intMantissa.TrimEnd('0');
        exp += origLen - intMantissa.Length;

        // Now: value = int(intMantissa) × 10^exp
        // ECMAScript spec defines:
        //   s = int(intMantissa), k = intMantissa.Length
        //   n = exp + k   (so that s × 10^(n-k) = value)
        int k = intMantissa.Length;
        int n = exp + k;

        // Step 6: If k ≤ n ≤ 21 → integer with trailing zeros
        if (k <= n && n <= 21)
            return intMantissa + new string('0', n - k);

        // Step 7: If 0 < n ≤ 21 → decimal (n < k here since step 6 handled n ≥ k)
        if (0 < n && n <= 21)
            return intMantissa.Substring(0, n) + "." + intMantissa.Substring(n);

        // Step 8: If -5 ≤ n ≤ 0 → "0.000...digits"
        if (-5 <= n && n <= 0)
            return "0." + new string('0', -n) + intMantissa;

        // Steps 9-10: Scientific notation
        int expVal = n - 1;
        string expStr = (expVal >= 0 ? "+" : "") + expVal.ToString(CultureInfo.InvariantCulture);
        if (k == 1)
            return intMantissa + "e" + expStr;

        return intMantissa[0] + "." + intMantissa.Substring(1) + "e" + expStr;
    }

    public override string ToLocaleString(string format, CultureInfo culture) => value.ToString(format, culture.NumberFormat);

    public override string ToDetailString() => value.ToString();


    public static bool IsNaN(JSValue n) => double.IsNaN(n.DoubleValue);

    public override JSValue Negate() => new JSNumber(-value);

    public override JSValue AddValue(JSValue value)
    {
        value = value.IsObject ? value.ValueOf() : value;

        if (value is JSPrimitiveObject po)
            value = po.value;

        if (value is JSString @string)
            return new JSString(ToECMAString(this.value) + @string.ToString());

        if (value is JSObject @object)
            return new JSString(ToECMAString(this.value) + @object.StringValue);

        return new JSNumber(this.value + value.DoubleValue);
    }

    public override JSValue AddValue(double value) => new JSNumber(this.value + value);

    public override JSValue AddValue(string value) => new JSString(ToECMAString(this.value) + value);

    public override int GetHashCode() => (int)value;
    public override bool Equals(object obj)
    {
        if (obj is JSNumber n)
        {
            if (double.IsNaN(value) || double.IsNaN(n.value))
                return false;

            return value == n.value;
        }

        return base.Equals(obj);
    }

    public override bool Equals(JSValue value)
    {
        if (ReferenceEquals(this, value))
        {
            if (double.IsNaN(this.value))
                return false;

            return true;
        }

        switch (value)
        {
            case JSNumber number:
                if (double.IsNaN(this.value) || double.IsNaN(number.value))
                    return false;
                if (this.value == number.value)
                    return true;
                return false;

            case JSString @string
                when this.value == @string.DoubleValue:
                return true;

            case JSNull _
                when this.value == 0D:
                return true;

            case JSValue boolVal when boolVal.IsBoolean && this.value == (boolVal.BooleanValue ? 1D : 0D):
                return true;
        }

        // Added for this TC ExpressionTests.cs Assert.AreEqual(true, Evaluate("2 == [2]"));
        if (ToString() == value.ToString())
            return true;

        return false;
    }

    public override bool StrictEquals(JSValue value)
    {

        if (ReferenceEquals(this, value))
        {
            if (double.IsNaN(this.value))
                return false;

            return true;
        }

        if (value is JSNumber n)
        {
            if (double.IsNaN(this.value) || double.IsNaN(n.value))
                return false;

            if (this.value == n.value)
                return true;
        }

        return false;
    }

    public override bool SameValueZero(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        if (value is JSNumber n)
        {
            if (double.IsNaN(this.value) && double.IsNaN(n.value))
                return true;

            if (this.value == n.value)
                return true;
        }

        return false;
    }

    public override bool EqualsLiteral(double value) => this.value == value;

    public override bool EqualsLiteral(string value) => this.value.ToString() == value || this.value == NumberParser.CoerceToNumber(value);

    public override bool StrictEqualsLiteral(double value) => this.value == value;

    public override JSValue InvokeFunction(in Arguments a) => throw JSEngine.NewTypeError($"{value} is not a function");

    internal override JSValue Is(JSValue value)
    {
        if (value is JSNumber number)
        {
            if (this.value == 0 || number.value == 0)
                return BitConverter.DoubleToInt64Bits(this.value) == BitConverter.DoubleToInt64Bits(number.value) ? JSValue.BooleanTrue : JSValue.BooleanFalse;

            if (double.IsNaN(this.value))
                return double.IsNaN(number.value) ? JSValue.BooleanTrue : JSValue.BooleanFalse;

            if (this.value == number.value)
                return JSValue.BooleanTrue;
        }

        return JSValue.BooleanFalse;
    }
}
