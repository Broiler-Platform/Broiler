using System;
using System.Collections.Generic;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Clr;

/// <summary>
/// Builds LINQ expression tree nodes that call <see cref="ClrProxy"/>
/// marshal/from methods.  Registered into
/// <see cref="LinqExpressions.LinqExpressions.ClrProxyBuilder"/>
/// by the assembly's module initializer.
/// </summary>
internal static class ClrExpressionBuilder
{
    private static readonly Dictionary<Type, MethodInfo> _marshal;
    private static readonly Dictionary<Type, MethodInfo> _from;

    static ClrExpressionBuilder()
    {
        var type = typeof(ClrProxy);

        var d = new Dictionary<Type, MethodInfo>(10);
        var marshal = nameof(ClrProxy.Marshal);

        foreach (var m in type.GetMethods())
        {
            if (m.Name != marshal)
                continue;

            d[m.GetParameters()[0].ParameterType] = m;
        }

        _marshal = d;

        var from = nameof(ClrProxy.From);
        d = new Dictionary<Type, MethodInfo>(10);

        foreach (var m in type.GetMethods())
        {
            if (m.Name != from)
                continue;

            if (m.GetParameters().Length != 1)
                continue;

            d[m.GetParameters()[0].ParameterType] = m;
        }

        _from = d;
    }

    public static YExpression Marshal(YExpression target)
    {
        if (_marshal.TryGetValue(target.Type, out var m))
            return YExpression.Call(null, m, target);

        if (target.Type.IsValueType)
            return YExpression.Call(null, _marshal[typeof(object)], YExpression.Box(target));

        return YExpression.Call(null, _marshal[typeof(object)], target);
    }

    public static YExpression From(YExpression target)
    {
        var targetType = target.Type;
        if (_from.TryGetValue(targetType, out var m))
            return YExpression.Call(null, m, target);

        if (targetType.IsValueType)
            return YExpression.Call(null, _from[typeof(object)], YExpression.Box(target));

        foreach (var pair in _from)
        {
            if (pair.Key.IsAssignableFrom(targetType))
                return YExpression.Call(null, pair.Value, target);
        }

        return YExpression.Call(null, _from[typeof(object)], target);
    }
}
