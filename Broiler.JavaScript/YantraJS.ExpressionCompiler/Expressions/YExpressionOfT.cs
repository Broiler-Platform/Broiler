using System;

namespace YantraJS.Expressions;

public class YExpression<T>(in FunctionName name, YExpression body, YParameterExpression @this, YParameterExpression[] parameters, Type returnType) : YLambdaExpression(typeof(T), in name, body, @this, parameters, returnType)
{
    internal YExpression<T1> WithThis<T1>(Type type)
    {
        if (This != null)
            throw new InvalidOperationException();
        return new YExpression<T1>(in Name, Body, YExpression.Parameter(type), Parameters, ReturnType);
    }
}
