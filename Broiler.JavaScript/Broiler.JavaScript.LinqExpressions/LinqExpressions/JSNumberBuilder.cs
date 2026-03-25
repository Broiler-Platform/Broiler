using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSNumberBuilder
{
    private static Type type;
    private static ConstructorInfo _ctor;

    public static Expression NaN;
    public static Expression Zero;
    public static Expression One;
    public static Expression MinusOne;
    public static Expression Two;

    /// <summary>
    /// Initializes the builder with the concrete JSNumber type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type numberType)
    {
        type = numberType;
        _ctor = type.GetConstructor([typeof(double)]);

        NaN = Expression.Field(null, type.GetField("NaN"));
        Zero = Expression.Field(null, type.GetField("Zero"));
        One = Expression.Field(null, type.GetField("One"));
        MinusOne = Expression.Field(null, type.GetField("MinusOne"));
        Two = Expression.Field(null, type.GetField("Two"));
    }

    public static Expression New(Expression exp)
    {
        if (exp.Type != typeof(double))
            exp = Expression.Convert(exp, typeof(double));

        return Expression.TypeAs(Expression.New(_ctor, exp), typeof(JSValue));
    }
}
