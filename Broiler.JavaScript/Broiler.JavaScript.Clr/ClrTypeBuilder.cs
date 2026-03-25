using System;
using System.Collections.Generic;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Runtime;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Clr;

internal static class ClrTypeBuilder
{
    internal delegate JSValue InstanceDelegate<T>(T @this, in Arguments a);

    internal delegate object ClrProxyFactory(in Arguments a);

    private static JSFunctionDelegate CreateInstanceDelegate<T>(this MethodInfo method)
    {
        var d = method.CreateDelegate<InstanceDelegate<T>>();
        var thisDelegate = JSValueToClrConverter.ToFastClrDelegate<T>();

        return (in Arguments a) =>
        {
            var @this = thisDelegate(a.This, "this");
            return d(@this, in a);
        };
    }
    internal static ClrProxyFactory CompileToJSFunctionDelegate(this ConstructorInfo m, string name = null)
    {
        var args = YExpression.Parameter(typeof(Arguments).MakeByRefType());
        var parameters = m.GetArgumentsExpression(args);
        YExpression body = YExpression.New(m, parameters);
        body = m.DeclaringType.IsValueType ? YExpression.Box(body) : body;
        var lambda = YExpression.Lambda<ClrProxyFactory>(name, body, args);
        return lambda.Compile();
    }

    internal static JSFunctionDelegate CompileToJSFunctionDelegate(this MethodInfo m, string name = null)
    {
        if (m.IsJSFunctionDelegate())
        {
            if (m.IsStatic)
            {
                return (JSFunctionDelegate)m.CreateDelegate(typeof(JSFunctionDelegate));
            }
            else
            {
                // we can directly create a delegate here...
                return Generic.InvokeAs(m.DeclaringType, CreateInstanceDelegate<object>, m);
            }
        }

        // We cannot use delegates as Arguments to CLR and CLR to JSValue
        // will be slower as it will use reflection internally to dispatch
        // actual conversion method.

        name ??= m.Name.ToCamelCase();

        // To speed up, we will use compilation.

        var args = YExpression.Parameter(typeof(Arguments).MakeByRefType());
        var parameters = m.GetArgumentsExpression(args);

        YExpression body;

        Type returnType;

        var @this = ArgumentsBuilder.This(args);
        var convertedThis = m.IsStatic ? null : JSValueToClrConverter.Get(@this, m.DeclaringType, "this");
        
        body = YExpression.Call(convertedThis, m, parameters);
        returnType = m.ReturnType;

        // unless return type is JSValue
        // we need to marshal it
        if (returnType == typeof(void))
        {
            body = YExpression.Block(body, JSUndefinedBuilder.Value);
        }
        else
        {
            body = ClrProxyBuilder.Marshal(body);
        }

        var lambda = YExpression.Lambda<JSFunctionDelegate>(name, body, args);
        return lambda.Compile();
    }

    private static List<YExpression> GetArgumentsExpression(this MethodBase m, YExpression args)
    {
        var parameters = new List<YExpression>();
        var pList = m.GetParameters();
        
        for (int i = 0; i < pList.Length; i++)
        {
            var ai = ArgumentsBuilder.GetAt(args, i);
            var pi = pList[i];
            YExpression defValue;
            
            if (pi.HasDefaultValue)
            {
                defValue = YExpression.Constant(pi.DefaultValue);
                if (pi.ParameterType.IsValueType)
                {
                    defValue = YExpression.Box(YExpression.Constant(pi.DefaultValue));
                }
                parameters.Add(JSValueToClrConverter.GetArgument(args, i, pi.ParameterType, defValue, pi.Name));
                continue;
            }
            
            defValue = null;
            
            if (pi.ParameterType.IsValueType)
            {
                defValue = YExpression.Constant(Activator.CreateInstance(pi.ParameterType));
            }
            else
            {
                defValue = YExpression.Null;
            }

            parameters.Add(JSValueToClrConverter.GetArgument(args, i, pi.ParameterType, defValue, pi.Name));
        }

        return parameters;
    }
}
