using System;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Runtime;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;

namespace Broiler.JavaScript.LinqExpressions;

public static class LinqExpressionsAssemblyInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Wire up JSValueToClrConverter expression delegates
        JSValueToClrConverter.GetAtExpression = ArgumentsBuilder.GetAt;
        JSValueToClrConverter.LengthExpression = ArgumentsBuilder.Length;
    }

    internal static object CreateClrDelegate(Type type, IJSFunction function)
    {
        var method = type.GetMethod("Invoke");
        var rt = method.ReturnType;
        var rtt = rt == typeof(void) ? typeof(object) : rt;
        var pa = method.GetParameters();
        var veList = new Sequence<ParameterExpression>(pa.Length + 1);
        var peList = new Sequence<ParameterExpression>(pa.Length);
        var stmts = new Sequence<Expression>();

        foreach (var p in method.GetParameters())
        {
            var inP = Expression.Parameter(p.ParameterType, p.Name);
            peList.Add(inP);

            var jsV = Expression.Parameter(typeof(JSValue), "js" + p.Name);
            veList.Add(jsV);

            stmts.Add(Expression.Assign(jsV, ClrProxyBuilder.Marshal(inP)));
        }

        var @delegate = function.Delegate;
        var d = Expression.Constant(@delegate);
        var @this = Expression.Constant((JSValue)function);
        var nargs = ArgumentsBuilder.New(@this, veList.AsSequence<Expression>());

        if (rt == typeof(void) || rt == typeof(object))
        {
            stmts.Add(Expression.Invoke(d, nargs));
        }
        else
        {
            stmts.Add(JSValueToClrConverter.Get(Expression.Invoke(d, nargs), rt, ""));
        }

        return Expression.Lambda(type, Expression.Block(veList, stmts), type.Name, peList.ToArray()).Compile();
    }
}
