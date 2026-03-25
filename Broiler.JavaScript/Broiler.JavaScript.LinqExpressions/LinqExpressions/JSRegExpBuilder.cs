using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSRegExpBuilder
{
    private static Type type;
    private static ConstructorInfo _ctor;

    /// <summary>
    /// Initializes the builder with the concrete JSRegExp type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type regExpType)
    {
        type = regExpType;
        _ctor = type.GetConstructor([typeof(string), typeof(string)])
            ?? throw new InvalidOperationException($"JSRegExp type {regExpType.FullName} does not have a (string, string) constructor.");
    }

    public static Expression New(Expression exp, Expression exp2) =>
        Expression.TypeAs(Expression.New(_ctor, exp, exp2), typeof(JSValue));
}
