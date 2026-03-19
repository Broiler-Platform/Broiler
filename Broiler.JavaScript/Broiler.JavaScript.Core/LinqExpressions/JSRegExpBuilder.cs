using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSRegExpBuilder
{
    public static Expression New(Expression exp, Expression exp2) => Expression.TypeAs(NewLambdaExpression.NewExpression<JSRegExp>(() => () => 
    new JSRegExp("", ""), exp, exp2), typeof(JSValue));
}
