using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;


public class JSStringBuilder
{
    public static Expression New(Expression exp) => Expression.TypeAs(NewLambdaExpression.NewExpression<JSString>(() => () => new JSString(""), exp), typeof(JSValue));
}
