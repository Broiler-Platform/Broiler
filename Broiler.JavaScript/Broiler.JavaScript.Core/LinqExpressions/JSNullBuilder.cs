using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSNullBuilder
{
    public static Expression Value = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSNull.Value);
}
