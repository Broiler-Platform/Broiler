using System;
using System.Linq.Expressions;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.LinqExpressions.LambdaGen;

public static class NewLambdaExpression
{
    public static Expression FieldExpression<TTarget, TTOut>(this Expression exp, Func<Expression<Func<TTarget, TTOut>>> func) where TTarget : class
    {
        var f = TypeQuery.TypeQuery.QueryInstanceField(func);
        if (f.IsStatic)
            return Expression.Field(null, f);

        return Expression.Field(exp, f);
    }

    public static Expression StaticFieldExpression<TTOut>(Func<Expression<Func<TTOut>>> func)
    {
        var f = TypeQuery.TypeQuery.QueryStaticField(func);
        return Expression.Field(null, f);
    }

    public static Expression PropertyExpression<TTarget, TTOut>(this Expression exp, Func<Expression<Func<TTarget, TTOut>>> func) where TTarget : class
    {
        var f = TypeQuery.TypeQuery.QueryInstanceProperty(func);
        return Expression.Property(exp, f);
    }

    public static YNewExpression NewExpression<TOut>(Func<Expression<Func<TOut>>> fx, params Expression[] args)
    {
        var m = TypeQuery.TypeQuery.QueryConstructor(fx);
        return Expression.New(m, args);
    }

    /// <summary>
    /// Creates a new-expression for the given type using the constructor whose
    /// parameter count matches <paramref name="args"/>.
    /// Used when the type is only known at runtime (e.g. after Initialize pattern).
    /// </summary>
    public static YNewExpression NewExpression(Type type, params Expression[] args)
    {
        var paramTypes = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            paramTypes[i] = args[i].Type;

        var ctor = type.GetConstructor(paramTypes);
        if (ctor == null)
        {
            // Fall back to finding a constructor with matching parameter count
            // and compatible types (handles in/ref parameters and implicit conversions).
            foreach (var c in type.GetConstructors())
            {
                var ps = c.GetParameters();
                if (ps.Length != args.Length)
                    continue;

                bool match = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (pt.IsByRef)
                        pt = pt.GetElementType();
                    var at = args[i].Type;
                    if (!pt.IsAssignableFrom(at) && !HasImplicitConversion(at, pt))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    ctor = c;
                    break;
                }
            }
        }

        if (ctor == null)
            throw new InvalidOperationException($"No constructor found on {type.FullName} matching {args.Length} parameters");

        return Expression.New(ctor, args);
    }

    private static bool HasImplicitConversion(Type from, Type to)
    {
        // Check for implicit operators
        foreach (var m in to.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (m.Name == "op_Implicit" && m.ReturnType == to && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == from)
                return true;
        }
        foreach (var m in from.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (m.Name == "op_Implicit" && m.ReturnType == to && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == from)
                return true;
        }
        return false;
    }

    public static Expression StaticCallExpression<TOut>(Func<Expression<Func<TOut>>> fx, params Expression[] args)
    {
        var m = TypeQuery.TypeQuery.QueryStaticMethod(fx);
        return Expression.Call(null, m, args);
    }

    public static Expression StaticCallExpression(Func<Expression<Action>> fx, params Expression[] args)
    {
        var m = TypeQuery.TypeQuery.QueryStaticMethod(fx);
        return Expression.Call(null, m, args);
    }

    public static Expression CallExpression<TIn, TOut>(this Expression @this, Func<Expression<Func<TIn, TOut>>> fx, params Expression[] args)
    {
        var m = TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, args);
    }

    public static Expression CallExpression<T>(this Expression @this, Func<Expression<Action<T>>> fx, params Expression[] args)
    {
        var m = TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, args);
    }

    public static Expression CallExpression<TIn, T, TOut>(this Expression @this, Func<Expression<Func<TIn, T, TOut>>> fx, Expression p1)
    {
        var m = TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, p1);
    }

    public static Expression CallExpression<TIn, T, TOut>(this Expression @this, Func<Expression<Action<TIn, T, TOut>>> fx, Expression p1, Expression p2)
    {
        var m = TypeQuery.TypeQuery.QueryInstanceMethod(fx);
        return Expression.Call(@this, m, p1, p2);
    }
}
