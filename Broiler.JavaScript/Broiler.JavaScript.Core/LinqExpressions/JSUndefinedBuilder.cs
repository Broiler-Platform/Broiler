using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core.Primitive;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSUndefinedBuilder
{
    public static Expression Value = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSUndefined.Value);
}
