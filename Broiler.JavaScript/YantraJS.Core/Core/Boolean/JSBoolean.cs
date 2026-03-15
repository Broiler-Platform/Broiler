using System;
using Yantra.Core;
using YantraJS.Core.Clr;

namespace YantraJS.Core;

[JSBaseClass("Object")]
[JSFunctionGenerator("Boolean")]
public partial class JSBoolean : JSPrimitive
{

    public static JSBoolean True = new(true);

    public static JSBoolean False = new(false);

    internal readonly bool _value;

    private JSBoolean(bool _value) : base() => this._value = _value;

    [JSExport(IsConstructor = true)]
    public static JSValue Constructor(in Arguments a) => (a[0]?.BooleanValue ?? false) ? True : False;

    protected override JSObject GetPrototype() => GetCurrentPrototype();


    //public static bool IsTrue(JSValue value)
    //{
    //    switch (value)
    //    {
    //        case JSString str:
    //            return str.Length > 0;
    //        case JSBoolean bv:
    //            return bv._value;
    //        case JSNumber n:
    //            return n.value != 0 && n.value != double.NaN;
    //        case JSObject obj:
    //            return true;
    //    }
    //    return false;
    //}

    public override double DoubleValue => _value ? 1 : 0;

    public override bool BooleanValue => _value;

    public override bool IsBoolean => true;

    public override JSValue TypeOf() => JSConstants.Boolean;

    public override JSValue Negate() => _value ? JSNumber.MinusOne : JSNumber.NegativeZero;

    public override bool ConvertTo(Type type, out object value)
    {
        if (type == typeof(bool))
        {
            value = _value;
            return true;
        }
        if (type.IsAssignableFrom(typeof(JSBoolean)))
        {
            value = this;
            return true;
        }
        if (type == typeof(object))
        {
            value = _value;
            return true;
        }

        value = null;
        return false;
    }

    public override string ToString() => _value ? "true" : "false";

    public override int GetHashCode() => _value ? 1 : 0;

    public override bool Equals(object obj)
    {
        if (obj is JSBoolean b)
            return _value == b._value;
        return base.Equals(obj);
    }

    public override bool Equals(JSValue value)
    {
        if (Object.ReferenceEquals(this, value))
            return true;
        if (!_value)
        {
            if (value.IsNullOrUndefined)
                return false;
        }
        if (_value) {
            if (value.DoubleValue == 1)
                return true;
        } else
        {
            if (value.DoubleValue == 0)
                return true;
        }
        return false;
    }

    public override bool EqualsLiteral(double value) => _value
            ? value == 1
            : value == 0;

    public override bool EqualsLiteral(string value) => _value
            ? value == "1"
            : value == "0";

    public override bool StrictEquals(JSValue value) => Object.ReferenceEquals(this, value);

    public override JSValue InvokeFunction(in Arguments a) => throw new NotImplementedException("boolean is not a function");

    internal override PropertyKey ToKey(bool create = false) => _value ? KeyStrings.@true : KeyStrings.@false;
}
