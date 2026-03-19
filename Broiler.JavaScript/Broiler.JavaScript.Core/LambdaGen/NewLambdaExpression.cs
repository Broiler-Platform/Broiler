using System;
using System.Linq.Expressions;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.LambdaGen;

internal static class NewLambdaExpression
{
    public static Expression FieldExpression<TTarget, TTOut>(this Expression exp, Func<Expression<Func<TTarget, TTOut>>> func) where TTarget : class
    {
        var f = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceField(func);
        if (f.IsStatic)
            return Expression.Field(null, f);

        return Expression.Field(exp, f);
    }

    public static Expression StaticFieldExpression<TTOut>(Func<Expression<Func<TTOut>>> func)
    {
        var f = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryStaticField(func);
        return Expression.Field(null, f);
    }

    public static Expression PropertyExpression<TTarget, TTOut>(this Expression exp, Func<Expression<Func<TTarget, TTOut>>> func) where TTarget : class
    {
        var f = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceProperty(func);
        return Expression.Property(exp, f);
    }

    public static YNewExpression NewExpression<TOut>(Func<Expression<Func<TOut>>> fx, params Expression[] args)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryConstructor(fx);
        return Expression.New(m, args);
    }

    public static Expression StaticCallExpression<TOut>(Func<Expression<Func<TOut>>> fx, params Expression[] args)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryStaticMethod(fx);
        return Expression.Call(null, m, args);
    }

    public static Expression StaticCallExpression(Func<Expression<Action>> fx, params Expression[] args)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryStaticMethod(fx);
        return Expression.Call(null, m, args);
    }

    public static Expression CallExpression<TIn, TOut>(this Expression @this, Func<Expression<Func<TIn, TOut>>> fx, params Expression[] args)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, args);
    }

    public static Expression CallExpression<T>(this Expression @this, Func<Expression<Action<T>>> fx, params Expression[] args)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, args);
    }

    public static Expression CallExpression<TIn, T, TOut>(this Expression @this, Func<Expression<Func<TIn, T, TOut>>> fx, Expression p1)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, p1);
    }

    public static Expression CallExpression<TIn, T, TOut>(this Expression @this, Func<Expression<Action<TIn, T, TOut>>> fx, Expression p1, Expression p2)
    {
        var m = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, p1, p2);
    }
}
