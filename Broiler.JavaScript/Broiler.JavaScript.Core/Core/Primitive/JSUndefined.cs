using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using System;
using System.Globalization;

namespace Broiler.JavaScript.Core.Core.Primitive;

public sealed class JSUndefined : JSValue
{
    private JSUndefined() : base(null) { }

    public static JSValue Value = new JSUndefined();

    internal override PropertyKey ToKey(bool create = true) => KeyStrings.undefined;

    public override JSValue TypeOf() => JSConstants.Undefined;

    public override bool BooleanValue => false;

    public override double DoubleValue => double.NaN;

    public override uint UIntValue => 0;

    public override int IntegerValue => 0;

    public override int IntValue => 0;

    public override string ToString() => "undefined";

    public override JSValue this[KeyString name]
    {
        get
        {
#if DEBUG
            var st = new System.Diagnostics.StackTrace(true);
            Console.Error.WriteLine($"[JSUndefined] Cannot get property {name} of undefined");
            Console.Error.WriteLine(st.ToString());
#endif
            throw JSContext.NewTypeError($"Cannot get property {name} of undefined");
        }
        set => throw JSContext.NewTypeError($"Cannot set property {name} of undefined");
    }

    public override JSValue this[uint key]
    {
        get => throw JSContext.NewTypeError($"Cannot get property {key} of undefined");
        set => throw JSContext.NewTypeError($"Cannot set property {key} of undefined");
    }

    internal override JSFunctionDelegate GetMethod(in KeyString key) => throw JSContext.NewTypeError($"Cannot get property {key} of undefined");

    public override JSValue Delete(in KeyString key) => throw JSContext.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

    public override JSValue Delete(uint key) => throw JSContext.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

    public override bool Equals(JSValue value) => value.IsNullOrUndefined;//if (value.IsUndefined)//    return true;//return false;

    public override bool StrictEquals(JSValue value) => ReferenceEquals(this, value);

    public override JSValue CreateInstance(in Arguments a) => throw JSContext.NewTypeError("cannot create instance of undefined");

    public override JSValue InvokeFunction(in Arguments a) => throw JSContext.NewTypeError("undefined is not a function", null);

    public override IElementEnumerator GetElementEnumerator() => throw JSContext.NewTypeError("undefined is not iterable");

    public override bool ConvertTo(Type type, out object value)
    {
        if (type.IsAssignableFrom(typeof(JSUndefined)))
        {
            value = this;
            return true;
        }

        value = null;
        return !type.IsValueType;
    }

    public override string ToLocaleString(string format, CultureInfo culture) => "";
}
