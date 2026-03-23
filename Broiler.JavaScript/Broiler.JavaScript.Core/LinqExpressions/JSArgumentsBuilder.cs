using System;
using System.Reflection;
using Broiler.JavaScript.Core.Core;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSArgumentsBuilder
{
    private static Type _type;
    private static ConstructorInfo _New;

    /// <summary>
    /// Initializes the builder with the concrete JSArguments type.
    /// Called by the Modules assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type argumentsType)
    {
        _type = argumentsType;
        _New = argumentsType.GetConstructor([typeof(Arguments).MakeByRefType()]);
    }

    public static Expression New(Expression args) => Expression.New(_New, args);
}
