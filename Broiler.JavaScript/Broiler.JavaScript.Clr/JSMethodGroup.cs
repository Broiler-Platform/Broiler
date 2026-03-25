using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Clr;

internal class JSMethodGroup
{
    public readonly string name;
    public readonly List<MethodInfo> Methods;
    public readonly MethodInfo JSMethod;
    private readonly Type type;
    private readonly (MethodInfo method, ParameterInfo[] parameters)[] all;

    public JSMethodGroup(ClrMemberNamingConvention namingConvention, Type type, IGrouping<string, MethodInfo> methods)
    {
        Methods = [.. methods];
        JSMethod = Methods.FirstOrDefault(x => x.IsJSFunctionDelegate());

        var (n, e) = ClrTypeExtensions.GetJSName(namingConvention, JSMethod ?? Methods.OrderByDescending(x => x.GetCustomAttribute<JSExportAttribute>()).First());

        name = n;
        all = new (MethodInfo method, ParameterInfo[] parameters)[Methods.Count];

        for (int i = 0; i < all.Length; i++)
        {
            var m = Methods[i];
            all[i] = (m, m.GetParameters());
        }

        this.type = type;
    }

    internal JSValue Generate(bool isStatic) => isStatic ? new JSFunction(StaticInvoke, name) : new JSFunction(Invoke, name);

    private JSValue Invoke(in Arguments a)
    {
        if (!a.This.ConvertTo(type, out var target))
            throw JSEngine.NewTypeError($"{type.Name}.prototype.{name} called with object not of type {type.Name}");

        try
        {
            var (method, args) = all.Match(a, name);
            return JSEngine.ClrInterop.Marshal(method.Invoke(target, args));
        }
        catch (Exception ex)
        {
            throw JSException.From(ex);
        }
    }

    private JSValue StaticInvoke(in Arguments a)
    {
        try
        {
            var (method, args) = all.Match(a, name);
            return JSEngine.ClrInterop.Marshal(method.Invoke(null, args));
        }
        catch (Exception ex)
        {
            throw JSException.From(ex);
        }
    }
}
