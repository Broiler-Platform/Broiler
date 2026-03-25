using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Engine.Extensions;

public static partial class JSValueExtensions
{
    public static bool ConvertTo<T>(this JSValue @this, out T value)
    {
        if (JSEngine.ClrInterop.TryUnwrapClrObject(@this, out var clrObj) && clrObj is T t)
        {
            value = t;
            return true;
        }

        if (@this.TryConvertTo(typeof(T), out var v) && v is T tv)
        {
            value = tv;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns .net string if it is not undefined
    /// </summary>
    /// <param name="target"></param>
    /// <param name="def"></param>
    /// <returns></returns>
    public static string AsStringOrDefault(this JSValue target, string def = null) => target.IsUndefined ? def : target.ToString();

    /// <summary>
    /// Returns .net int if it is not undefined
    /// </summary>
    /// <param name="target"></param>
    /// <param name="def"></param>
    /// <returns></returns>
    public static int AsInt32OrDefault(this JSValue target, int def = 0) => target.IsUndefined ? def : target.IntValue;

    /// <summary>
    /// Returns .net double if it is not undefined
    /// </summary>
    /// <param name="target"></param>
    /// <param name="def"></param>
    /// <returns></returns>
    public static double AsDoubleOrDefault(this JSValue target, double def = 0) => target.IsUndefined ? def : target.DoubleValue;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue InvokeMethod(this JSValue @this, in KeyString name, in Arguments a)
    {
        var fx = @this.GetMethod(in name);
        return fx == null ? throw JSEngine.NewTypeError($"Method {name} not found in {@this}") : fx(a.OverrideThis(@this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx) => fx.InvokeFunction(in Arguments.Empty);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx, JSValue @this)
    {
        var a = new Arguments(@this);
        return fx.InvokeFunction(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx, JSValue @this, JSValue arg0)
    {
        var a = new Arguments(@this, arg0);
        return fx.InvokeFunction(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx, JSValue @this, JSValue arg0, JSValue arg1)
    {
        var a = new Arguments(@this, arg0, arg1);
        return fx.InvokeFunction(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx, JSValue @this, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        var a = new Arguments(@this, arg0, arg1, arg2);
        return fx.InvokeFunction(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx, JSValue @this, JSValue arg0, JSValue arg1, JSValue arg2, JSValue arg3)
    {
        var a = new Arguments(@this, arg0, arg1, arg2, arg3);
        return fx.InvokeFunction(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue Call(this JSValue fx, JSValue @this, JSValue[] args)
    {
        var a = new Arguments(@this, args);
        return fx.InvokeFunction(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue CreateInstance(this JSValue @fx) => fx.CreateInstance(in Arguments.Empty);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue CreateInstance(this JSValue @fx, JSValue arg0)
    {
        var a = new Arguments(JSUndefined.Value, arg0);
        return fx.CreateInstance(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue CreateInstance(this JSValue fx, JSValue arg0, JSValue arg1)
    {
        var a = new Arguments(JSUndefined.Value, arg0, arg1);
        return fx.CreateInstance(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue CreateInstance(this JSValue fx, JSValue arg0, JSValue arg1, JSValue arg2)
    {
        var a = new Arguments(JSUndefined.Value, arg0, arg1, arg2);
        return fx.CreateInstance(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue CreateInstance(this JSValue fx, JSValue arg0, JSValue arg1, JSValue arg2, JSValue arg3)
    {
        var a = new Arguments(JSUndefined.Value, arg0, arg1, arg2, arg3);
        return fx.CreateInstance(in a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue CreateInstance(this JSValue fx, JSValue[] args)
    {
        var a = new Arguments(JSUndefined.Value, args);
        return fx.CreateInstance(in a);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue InvokeMethod(this JSValue @this, uint name, in Arguments a)
    {
        var fx = @this[name];
        if (fx.IsUndefined)
            throw JSEngine.NewTypeError($"Method {name} not found on {@this}");

        return fx.InvokeFunction(a.OverrideThis(@this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue InvokeMethod(this JSValue @this, IJSSymbol name, in Arguments a)
    {
        var fx = @this[name];
        if (fx.IsUndefined)
            throw JSEngine.NewTypeError($"Method {name} not found on {@this}");

        return fx.InvokeFunction(a.OverrideThis(@this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue InvokeMethod(this JSValue @this, JSValue name, in Arguments a)
    {
        var key = name.ToKey();
        if (key.IsUInt)
            return @this.InvokeMethod(key.Index, a);

        if (key.IsSymbol)
            return @this.InvokeMethod(key.Symbol, a);

        return @this.InvokeMethod(in key.KeyString, a);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue NullIfUndefined(JSValue value)
    {
        if (value.IsUndefined)
            return null;

        return value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue NullIfUndefinedOrNull(JSValue value)
    {
        if (value == JSValue.NullValue || value == JSUndefined.Value)
            return null;

        return value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue NullIfTrue(JSValue value)
    {
        if (value.BooleanValue)
            return null;

        return value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue NullIfFalse(JSValue value)
    {
        if (!value.BooleanValue)
            return null;

        return value;
    }

    public static JSValue InstanceOf(this JSValue target, JSValue value)
    {
        if (value.IsUndefined)
            throw JSEngine.NewTypeError("Right side of instanceof is undefined");

        if (value.IsNull)
            throw JSEngine.NewTypeError("Right side of instanceof is null");

        if (!value.IsFunction)
            throw JSEngine.NewTypeError("Right side of instanceof is not a function");

        var p = (target.prototypeChain as IJSPrototype)?.Object as JSObject;
        if (p == null)
            return JSValue.BooleanFalse;

        var c = p[KeyStrings.constructor];
        if (c.IsUndefined)
            return JSValue.BooleanFalse;

        if (c.StrictEquals(value))
            return JSValue.BooleanTrue;

        return p.InstanceOf(value);
    }

    public static JSValue IsIn(this JSValue target, JSValue value)
    {
        if (value is not JSObject tx)
            throw JSEngine.NewTypeError($"Cannot use 'in' operator to search for '{target}' in {value}");

        var key = target.ToKey(false);
        if (key.IsUInt)
        {
            var p = tx.GetInternalProperty(key.Index);
            if (!p.IsEmpty)
                return JSValue.BooleanTrue;

            return JSValue.BooleanFalse;
        }

        var p1 = tx.GetInternalProperty(in key.KeyString);
        if (!p1.IsEmpty)
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }
}
