using System;
using System.Globalization;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Null;

public sealed class JSNull : JSValue
{
    private JSNull() : base(null) { }

    public override JSValue TypeOf() => JSConstants.Object;

    public static JSValue Value = new JSNull();

    public override string ToString() => "null";

    public override bool BooleanValue => false;

    public override double DoubleValue => 0D;

    public override uint UIntValue => 0;

    public override int IntegerValue => 0;

    public override int IntValue => 0;

    public override JSValue Negate() => JSValue.NumberNegativeZero;

    internal override PropertyKey ToKey(bool create = false) => KeyStrings.@null;

    public override bool Equals(object obj) => obj is JSNull;

    public override JSValue Delete(in KeyString key) => throw JSEngine.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

    public override JSValue Delete(uint key) => throw JSEngine.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

    public override JSValue this[KeyString name]
    {
        get
        {
#if DEBUG
            var st = new System.Diagnostics.StackTrace(true);
            Console.Error.WriteLine($"[JSNull] Cannot get property {name} of null");
            Console.Error.WriteLine(st.ToString());
#endif
            throw JSEngine.NewTypeError($"Cannot get property {name} of null");
        }
        set => throw JSEngine.NewTypeError($"Cannot set property {name} of null");
    }

    public override JSValue this[uint key]
    {
        get => throw JSEngine.NewTypeError($"Cannot get property {key} of null");
        set => throw JSEngine.NewTypeError($"Cannot get property {key} of null");
    }

    internal override JSFunctionDelegate GetMethod(in KeyString key) => throw JSEngine.NewTypeError($"Cannot get property {key} of null");


    public override IElementEnumerator GetElementEnumerator() => throw JSEngine.NewTypeError("null is not iterable");


    public override int GetHashCode() => 0;

    public override bool Equals(JSValue value)
    {
        if (value.IsNull)
            return true;

        if (value.IsUndefined)
            return true;

        return false;
    }

    public override bool StrictEquals(JSValue value) => ReferenceEquals(this, value);

    public override JSValue CreateInstance(in Arguments a) => throw JSEngine.NewTypeError("cannot create instance of null");

    public override JSValue InvokeFunction(in Arguments a) => throw new NotImplementedException("null is not a function");

    public override bool ConvertTo(Type type, out object value)
    {
        if (type.IsAssignableFrom(typeof(JSNull)))
        {
            value = this;
            return true;
        }

        value = null;
        return !type.IsValueType;
    }

    public override string ToLocaleString(string format, CultureInfo culture) => "";
}
