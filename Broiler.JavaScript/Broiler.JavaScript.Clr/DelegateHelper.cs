using System;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Extensions;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Clr;

public static class DelegateHelper
{
    public static JSFunctionDelegate CreateSetter<T, TInput>(MethodInfo m)
    {
        var fx = m.CreateDelegate<Action<T, TInput>>();
        return (in Arguments a) =>
        {
            var input = a.Get1();
            var t = a.This.ToFastClrValue<T>();
            fx(t, (TInput)input.ForceConvert(typeof(TInput)));
            return input;
        };
    }

    public static JSFunctionDelegate CreateJSValueSetter<T>(MethodInfo m)
    {
        var fx = m.CreateDelegate<Action<T, JSValue>>();
        return (in Arguments a) =>
        {
            var input = a.Get1();
            var t = (T)a.This.ForceConvert(typeof(T));
            fx(t, input);
            return input;
        };
    }

    public static JSFunctionDelegate CreateGetter<T, TRet>(MethodInfo m)
    {
        var fx = m.CreateDelegate<Func<T, TRet>>();
        return (in Arguments a) =>
        {
            var t = (T)a.This.ForceConvert(typeof(T));
            return fx(t).Marshal();
        };
    }

    public static JSFunctionDelegate CreateJSValueGetter<T>(MethodInfo m)
    {
        var fx = m.CreateDelegate<Func<T, JSValue>>();
        return (in Arguments a) =>
        {
            var t = (T)a.This.ForceConvert(typeof(T));
            return fx(t);
        };
    }

    public static JSFunctionDelegate CreatePropertySetter(PropertyInfo property)
    {
        if (typeof(JSValue).IsAssignableFrom(property.PropertyType))
            return Generic.InvokeAs(property.DeclaringType, CreateJSValueSetter<object>, property.SetMethod);

        return Generic.InvokeAs(property.DeclaringType, property.PropertyType, CreateSetter<object, object>, property.SetMethod);
    }

    public static JSFunctionDelegate CreatePropertyGetter(PropertyInfo property)
    {
        if (typeof(JSValue).IsAssignableFrom(property.PropertyType))
            return Generic.InvokeAs(property.DeclaringType, CreateJSValueGetter<object>, property.GetMethod);

        return Generic.InvokeAs(property.DeclaringType, property.PropertyType, CreateGetter<object, object>, property.GetMethod);
    }

    public static JSFunctionDelegate Method<T>(MethodInfo m)
        where T : class
    {
        var fx = m.CreateDelegate<StaticDelegate<T>>();
        return (in Arguments a) =>
        {
            var @this = a.This.ForceConvert(typeof(T)) as T ?? throw JSEngine.NewTypeError($"this is not of type {typeof(T).Name}");
            return fx(@this, in a);
        };
    }

    public delegate JSValue StaticDelegate<T>(T target, in Arguments a);
    public static JSFunctionDelegate CreateMethod(MethodInfo method) => Generic.InvokeAs(method.DeclaringType, Method<object>, method);
}
