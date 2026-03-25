using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;


public class JSStringBuilder
{
    private static Type type;
    private static ConstructorInfo _ctor;

    /// <summary>
    /// Initializes the builder with the concrete JSString type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type stringType)
    {
        type = stringType;
        _ctor = type.GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException($"JSString type {stringType.FullName} does not have a (string) constructor.");
    }

    public static Expression New(Expression exp)
    {
        return Expression.TypeAs(Expression.New(_ctor, exp), typeof(JSValue));
    }
}
