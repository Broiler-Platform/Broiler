using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSUndefinedBuilder
{
    public static Expression Value = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSUndefined.Value);
}
