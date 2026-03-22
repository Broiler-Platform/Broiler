using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Enumerators;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.Core.Utils;
using Broiler.JavaScript.ExpressionCompiler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
namespace Broiler.JavaScript.Core;

public partial class JSObject
{
    public override JSValue TypeOf() => JSConstants.Object;
    public override JSValue ValueOf()
    {
        var p = GetInternalProperty(KeyStrings.valueOf, false);
        if (!p.IsEmpty)
            return this.GetValue(p).InvokeFunction(new Arguments(this));

        return this;
    }

    public bool HasValueOf(out JSValue value)
    {
        var p = GetInternalProperty(KeyStrings.valueOf, false);
        if (!p.IsEmpty)
        {
            value = this.GetValue(p).InvokeFunction(new Arguments(this));
            return true;
        }

        value = null;
        return false;

    }
    public override string ToString()
    {
        var px = GetMethod(KeyStrings.toString);
        if (px != null)
        {
            if (toStringCalled)
                return "Stack overflow";

            toStringCalled = true;

            var v = px(new Arguments(this));
            if (v != this)
            {
                toStringCalled = false;
                return v.ToString();
            }
        }

        return "[object Object]";
    }

    // prevent recursive...
    public override string ToDetailString()
    {
        var sb = new StringBuilder();
        bool first = true;
        sb.Append('{');

        foreach (var (Key, Value) in this.GetAllEntries(false))
        {
            if (Value == this)
                continue;
            if (!first)
                sb.Append(", ");
            first = false;
            sb.Append($"{Key}: {Value?.ToDetailString()}");
        }

        sb.Append('}');
        return sb.ToString();
    }
    public override bool Equals(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        if (value is JSString str)
            if (str.value.Equals(ToString()))
                return true;

        if (DoubleValue == value.DoubleValue)
            return true;

        return false;
    }

    public override bool EqualsLiteral(double value) => DoubleValue == value;

    public override bool EqualsLiteral(string value) => ToString() == value;

    public override bool StrictEquals(JSValue value) => ReferenceEquals(this, value);

    public override JSValue InvokeFunction(in Arguments a) => throw JSContext.NewTypeError($"{this} is not a function");

    public override bool Less(JSValue value)
    {
        switch (value)
        {
            case JSString strValue:
                if (ToString().CompareTo(strValue.ToString()) < 0)
                    return true;
                break;
        }

        return false;
    }

    public override bool LessOrEqual(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        return value switch
        {
            JSString strValue when ToString().CompareTo(strValue.ToString()) <= 0 => true,
            _ => false,
        };
    }

    public override bool Greater(JSValue value)
    {
        return value switch
        {
            JSString strValue when ToString().CompareTo(strValue.ToString()) > 0 => true,
            _ => false,
        };
    }

    public override bool GreaterOrEqual(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        return value switch
        {
            JSString strValue when ToString().CompareTo(strValue.ToString()) >= 0 => true,
            _ => false,
        };
    }
    public override bool ConvertTo(Type type, out object value)
    {
        if (this.TryGetClrEnumerator(type, out value))
            return true;

        if (type == typeof(object))
        {
            value = this;
            return true;
        }

        if (type != typeof(Type))
        {
            // if type has default constructor...
            if (this.TryUnmarshal(type, out value))
                return true;
        }

        return base.ConvertTo(type, out value);
    }
}
