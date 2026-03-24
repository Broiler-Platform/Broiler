using System;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSNullBuilder
{
    public static Expression Value = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSValue.NullValue);

    /// <summary>
    /// Initializes the builder with the concrete JSNull type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type nullType)
    {
        Value = Expression.Field(null, nullType.GetField("Value"));
    }
}
