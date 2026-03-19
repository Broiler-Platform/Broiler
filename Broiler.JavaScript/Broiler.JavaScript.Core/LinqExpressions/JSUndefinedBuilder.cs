using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSUndefinedBuilder
{
    public static Expression Value = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSUndefined.Value);
}
