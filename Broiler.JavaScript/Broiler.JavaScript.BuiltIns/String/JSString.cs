using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Ast.Misc;
using System;
using System.Globalization;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.String;

[JSBaseClass("Object")]
[JSFunctionGenerator("String")]
public partial class JSString : JSPrimitive
{
    internal static JSString Empty = new(string.Empty);

    internal readonly string value;

    /// <summary>
    /// Gets the underlying string value of this JSString instance.
    /// </summary>
    public new string StringValue => value;

    KeyString _keyString;

    private double NumberValue = 0;
    private bool NumberParsed = false;

    public override double DoubleValue
    {
        get
        {
            if (NumberParsed)
                return NumberValue;

            NumberValue = NumberParser.CoerceToNumber(value);
            NumberParsed = true;

            return NumberValue;
        }
    }

    public override bool BooleanValue => value.Length > 0;
    public override long BigIntValue => long.TryParse(ToString(), out var n) ? n : 0;
    public override bool IsString => true;

    public override JSValue AddValue(double value)
    {
        var numStr = JSValue.NumberToECMAString(value);

        if (this.value.IsEmpty())
            return new JSString(numStr);

        return new JSString(string.Concat(this.value, numStr));
    }

    public override JSValue AddValue(string value)
    {
        if (this.value.IsEmpty())
            return new JSString(value);

        return new JSString(string.Concat(this.value, value));
    }

    public override JSValue AddValue(JSValue value)
    {
        if (value is JSString vString)
        {
            if (this.value.IsEmpty())
                return vString;

            if (vString.value.IsEmpty())
                return this;

            return new JSString(string.Concat(this.value, vString.value));
        }

        if (value.IsObject)
            value = value.ValueOf();

        if (this.value.IsEmpty())
            return new JSString(value.StringValue);

        var v = value.StringValue;
        if (v.Length == 0)
            return this;

        return new JSString(string.Concat(this.value, v));
    }

    public override bool ConvertTo(Type type, out object value)
    {
        if (type == typeof(string))
        {
            value = this.value;
            return true;
        }

        if (type == typeof(object))
        {
            value = this.value;
            return true;
        }

        if (type == typeof(char))
        {
            value = this.value[0];
            return true;
        }

        if (type.IsAssignableFrom(typeof(JSString)))
        {
            value = this;
            return true;
        }

        value = null;
        return false;
    }

    internal override PropertyKey ToKey(bool create = true)
    {
        if (_keyString.HasValue)
            return _keyString;

        var d = DoubleValue;
        if (!double.IsNaN(d))
        {
            if (d >= 0 && (d % 1 == 0))
                return (uint)d;
        }

        if (!create)
        {
            if (!KeyStrings.TryGet(value, out _keyString))
                return KeyStrings.undefined;

            return _keyString;
        }

        return _keyString.Value != null ? _keyString : (_keyString = KeyStrings.GetOrCreate(value));
    }

    protected override JSValue GetPrototype() => ((JSEngine.Current as JSObject)?[Names.String] as JSFunction).prototype;

    public JSString(string value) : base() => this.value = value;
    public JSString(JSObject prototype, string value) : base(prototype) => this.value = value;

    public JSString(in StringSpan value) : base() => this.value = value.Value;


    public JSString(char ch) : this(new string(ch, 1)) { }


    public JSString(in StringSpan value, KeyString keyString) : this(value) => _keyString = keyString;

    public static implicit operator KeyString(JSString value) => value.ToString();

    public override JSValue TypeOf() => JSConstants.String;

    public override string ToString() => value;

    public byte[] Encode(System.Text.Encoding encoding) => encoding.GetBytes(value);

    public override string ToDetailString() => value;

    public override string ToLocaleString(string format, CultureInfo culture) => value;

    public override JSValue GetValue(uint key, JSValue receiver, bool throwError = true)
    {
        if (key >= value.Length)
            return JSUndefined.Value;

        return new JSString(new string(value[(int)key], 1));
    }

    public override IElementEnumerator GetAllKeys(bool showEnumerableOnly = true, bool inherited = true) => new IntKeyEnumerator(Length);

    [JSExport]
    public override int Length => value.Length;

    public override int GetHashCode() => value.GetHashCode();

    public override bool Equals(object obj)
    {
        if (obj is JSString v)
            return value == v.value;

        return base.Equals(obj);
    }

    public override bool Equals(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        switch (value)
        {
            case JSString strValue:
                if (this.value == strValue.value)
                    return true;
                return false;

            case JSValue number
                when number.IsNumber
                    && (DoubleValue == number.DoubleValue
                        || this.value.CompareTo(number.DoubleValue.ToString()) == 0):
                return true;

            case JSValue boolVal when boolVal.IsBoolean && DoubleValue == (boolVal.BooleanValue ? 1D : 0D):
                return true;
        }

        return false;
    }

    public override bool EqualsLiteral(double value) => DoubleValue == value || this.value.CompareTo(value.ToString()) == 0;

    public override bool EqualsLiteral(string value) => this.value.Equals(value);

    public override bool StrictEqualsLiteral(string value) => this.value.Equals(value);

    public override bool Less(JSValue value)
    {
        if (value.IsUndefined)
            return false;

        if (value.CanBeNumber)
            return DoubleValue < value.DoubleValue;

        return this.value.Less(value.ToString());

    }

    public override bool LessOrEqual(JSValue value)
    {
        if (value.IsUndefined)
            return false;

        if (value.CanBeNumber)
            return DoubleValue <= value.DoubleValue;

        return this.value.LessOrEqual(value.ToString());
    }

    public override bool Greater(JSValue value)
    {
        if (value.IsUndefined)
            return false;

        if (value.CanBeNumber)
            return DoubleValue > value.DoubleValue;

        return this.value.Greater(value.ToString());
    }

    public override bool GreaterOrEqual(JSValue value)
    {
        if (value.IsUndefined)
            return false;

        if (value.CanBeNumber)
            return DoubleValue >= value.DoubleValue;

        return this.value.GreaterOrEqual(value.ToString());
    }

    public override bool StrictEquals(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        if (value is JSString s)
            if (s.value.Equals(this.value))
                return true;

        return false;
    }

    public override JSValue InvokeFunction(in Arguments a) => throw new NotImplementedException($"\"{value}\" is not a function");

    internal override JSValue Is(JSValue value)
    {
        if (value is JSString @string && this.value == @string.value)
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    public override IElementEnumerator GetElementEnumerator() => new ElementEnumerator(value);

    private struct ElementEnumerator(in StringSpan value) : IElementEnumerator
    {
        private StringSpan.CharEnumerator en = value.GetEnumerator();
        int index = -1;

        public bool MoveNext(out bool hasValue, out JSValue value, out uint i)
        {
            if (en.MoveNext(out var ch))
            {
                index++;
                i = (uint)index;
                hasValue = true;
                value = new JSString(new string(ch, 1));
                return true;
            }

            i = 0;
            value = JSUndefined.Value;
            hasValue = false;
            
            return false;
        }

        public bool MoveNext(out JSValue value)
        {
            if (en.MoveNext(out var ch))
            {
                index++;
                value = new JSString(new string(ch, 1));
                return true;
            }

            value = JSUndefined.Value;
            return false;
        }

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            if (en.MoveNext(out var ch))
            {
                index++;
                value = new JSString(new string(ch, 1));
                return true;
            }

            value = @default;
            return false;
        }

        public JSValue NextOrDefault(JSValue @default)
        {
            if (en.MoveNext(out var ch))
            {
                index++;
                return new JSString(new string(ch, 1));
            }

            return @default;
        }
    }
}
