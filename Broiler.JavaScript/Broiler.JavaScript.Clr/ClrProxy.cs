using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Clr;

public partial class ClrProxy : JSObject
{
    public object Target => value;

    internal readonly object value;

    private ClrProxy(object value)
    {
        this.value = value;
        BasePrototypeObject = ClrType.From(value.GetType()).prototype;
    }

    private ClrProxy(object value, JSObject prototypeChain)
    {
        this.value = value;
        BasePrototypeObject = prototypeChain;
    }

    public override bool BooleanValue => value != null;

    /// <summary>
    /// Todo improvise...
    /// </summary>
    public override double DoubleValue => value == null ? 0 : NumberParser.CoerceToNumber(value.ToString());

    public override string ToString() => value.ToString();

    public override bool ConvertTo(Type type, out object value)
    {
        if (type.IsAssignableFrom(this.value.GetType()))
        {
            value = this.value;
            return true;
        }

        return base.ConvertTo(type, out value);
    }

    internal static MethodInfo[] methods = typeof(ClrProxy).GetMethods().Where(x => x.Name == "Marshal"
            && x.IsPublic
            && x.ReturnType == typeof(JSValue)
            && x.GetParameters().Length == 1
            && x.GetParameters()[0].ParameterType != typeof(object)).ToArray();

    public static Func<TInput, JSValue> GetDelegate<TInput>()
    {
        var method = methods.FirstOrDefault(x => x.GetParameters()[0].ParameterType == typeof(TInput));

        if (method != null)
            return (Func<TInput, JSValue>)method.CreateDelegate(typeof(Func<TInput, JSValue>));

        return (input) => Marshal(input);
    }

    public static JSValue Marshal(int value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(uint value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(long value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(ulong value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(string value) => JSValue.CreateString(value);
    public static JSValue Marshal(in StringSpan value) => JSValue.CreateString(value.Value);
    public static JSValue Marshal(bool value) => value ? JSValue.BooleanTrue : JSValue.BooleanFalse;
    public static JSValue Marshal(short value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(ushort value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(byte value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(sbyte value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(DateTime value) => JSValue.CreateDate(new DateTimeOffset(value));
    public static JSValue Marshal(DateTimeOffset value) => JSValue.CreateDate(value);
    public static JSValue Marshal(double value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(float value) => JSValue.CreateNumber(value);
    public static JSValue Marshal(Task task) => JSValue.CreatePromiseFromUntypedTask(task);
    public static JSValue Marshal(Task<JSValue> task) => JSValue.CreatePromiseFromTask(task);
    public static JSValue Marshal<T>(Task<T> task) => JSValue.CreatePromiseFromGenericTask(task);
    public static JSValue Marshal(IJavaScriptObject javaScriptObject) => From(javaScriptObject);
    public static JSValue Marshal(IElementEnumerator en) => JSGeneratorBuilder.CreateFromEnumerator(en, "Clr Iterator");
    public static JSValue Marshal(IEnumerable<JSValue> en) => JSGeneratorBuilder.CreateFromEnumerator(new ClrEnumerableElementEnumerator(en), "Clr Iterator");

    /// <summary>
    /// Can be improved in future !!
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static JSValue Marshal(object value)
    {
        if (value == null)
            return JSValue.NullValue;

        var type = value.GetType();

        if (type.IsEnum)
            return JSValue.CreateString(value.ToString());

        var t = Type.GetTypeCode(type);

        return t switch
        {
            TypeCode.Boolean => (bool)value ? JSValue.BooleanTrue : JSValue.BooleanFalse,
            TypeCode.Byte => JSValue.CreateNumber((byte)value),
            TypeCode.Char => JSValue.CreateString(((char)value).ToString()),
            TypeCode.DateTime => JSValue.CreateDate(new DateTimeOffset((DateTime)value)),
            TypeCode.DBNull => JSValue.NullValue,
            TypeCode.Decimal => JSValue.CreateNumber((double)(decimal)value),
            TypeCode.Double => JSValue.CreateNumber((double)value),
            TypeCode.Int16 => JSValue.CreateNumber((short)value),
            TypeCode.Int32 => JSValue.CreateNumber((int)value),
            TypeCode.Int64 => JSValue.CreateNumber((long)value),
            TypeCode.SByte => JSValue.CreateNumber((sbyte)value),
            TypeCode.Single => JSValue.CreateNumber((float)value),
            TypeCode.String => JSValue.CreateString((string)value),
            TypeCode.UInt16 => JSValue.CreateNumber((ushort)value),
            TypeCode.UInt32 => JSValue.CreateNumber((uint)value),
            TypeCode.UInt64 => JSValue.CreateNumber((long)value),
            _ => value switch
            {
                JSValue jsValue => jsValue,
                DateTimeOffset dateTimeOffset => JSValue.CreateDate(dateTimeOffset),
                Type valueType => ClrType.From(valueType),
                Task<JSValue> task => JSValue.CreatePromiseFromTask(task),
                Task task => JSValue.CreatePromiseFromUntypedTask(task),
                IJavaScriptObject obj => From(obj),
                IEnumerable<JSValue> en => JSGeneratorBuilder.CreateFromEnumerator(new ClrEnumerableElementEnumerator(en), "Clr Iterator"),
                _ => From(value),
            },
        };
    }

    public override IEnumerable<(string Key, JSValue value)> Entries
    {
        get
        {
            var en = new PropertyValueEnumerator(this, false);

            while (en.MoveNext(out var value, out var key))
                yield return (KeyStrings.GetNameString(key.Key).Value, value);
        }
    }

    public override bool Equals(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        if (value is ClrProxy proxy)
        {
            if (this.value == proxy.value)
                return true;

            if (this.value.Equals(proxy.value))
                return true;

            // convert to string to compare...
            if (this.value.ToString() == proxy.value.ToString())
                return true;
        }

        return false;
    }

    public override bool StrictEquals(JSValue value)
    {
        if (ReferenceEquals(this, value))
            return true;

        switch (value)
        {
            case ClrProxy proxy:
                if (this.value == proxy.value)
                    return true;
                if (this.value.Equals(proxy.value))
                    return true;
                break;

            case var @string when @string.IsString && @string.StringValue.Equals(this.value):
                return true;

            case JSValue number when number.IsNumber:
                switch (this.value)
                {
                    case int @int when @int == (int)number.DoubleValue:
                        return true;
                    case uint @uint when @uint == (uint)number.DoubleValue:
                        return true;
                    case long @long when @long == (long)number.DoubleValue:
                        return true;
                    case ulong @ulong when @ulong == (ulong)number.DoubleValue:
                        return true;
                    case double @double when @double == number.DoubleValue:
                        return true;
                    case float @float when @float == (float)number.DoubleValue:
                        return true;
                }
                break;
        }

        return false;
    }

    public override JSValue GetValue(uint key, JSValue receiver, bool throwError = true)
    {
        if (prototypeChain?.Object is ClrType.ClrPrototype p)
            return p.GetElementAt(value, key);

        return base.GetValue(key, receiver, throwError);
    }

    public override bool SetValue(uint name, JSValue value, JSValue receiver, bool throwError = true)
    {
        if (prototypeChain?.Object is ClrType.ClrPrototype p)
        {
            p.SetElementAt(this.value, name, value);
            return true;
        }

        return base.SetValue(name, value, receiver, throwError);
    }

    public override IElementEnumerator GetElementEnumerator()
    {
        if (value is IEnumerable<JSValue> jve)
            return new ClrEnumerableElementEnumerator(jve);

        if (value is IEnumerable en)
            return new EnumerableElementEnumerable(en.GetEnumerator());

        throw JSEngine.NewTypeError($"{this} is not an iterable");
    }

    public static ClrProxy From(int value) => new(value);
    public static ClrProxy From(string value) => new(value);
    public static ClrProxy From(bool value) => new(value);

    public static JSValue From(IJavaScriptObject value)
    {
        value.JSHandle ??= From(value, ClrType.From(value.GetType()));
        return value.JSHandle;
    }

    public static ClrProxy From(DateTimeOffset value) => new(value);

    private static readonly ConditionalWeakTable<object, ClrProxy> weakTable = [];

    public static JSValue From(object value)
    {
        if (value == null)
            return JSValue.NullValue;

        if (value is IJavaScriptObject scriptObject)
            return From(scriptObject);

        var type = ClrType.From(value.GetType());
        return From(value, type.prototype);
    }

    public static JSValue From(object value, JSObject prototype)
    {
        if (value == null)
            return JSValue.NullValue;

        if (value is IJavaScriptObject javaScriptObject)
        {
            if (javaScriptObject.JSHandle == null)
            {
                var type = ClrType.From(value.GetType());
                javaScriptObject.JSHandle = new ClrProxy(value, type.prototype);
            }

            return javaScriptObject.JSHandle;
        }

        if (value.GetType().IsValueType)
            return new ClrProxy(value, prototype);

        lock (weakTable)
        {
            if (!weakTable.TryGetValue(value, out var result))
            {
                result = new ClrProxy(value, prototype);
                weakTable.Add(value, result);
            }

            return result;
        }
    }
}
