using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSNumberBuilder
{
    public static Expression NaN = NewLambdaExpression.StaticFieldExpression<JSNumber>(() => () => JSNumber.NaN);
    public static Expression Zero = NewLambdaExpression.StaticFieldExpression<JSNumber>(() => () => JSNumber.Zero);
    public static Expression One = NewLambdaExpression.StaticFieldExpression<JSNumber>(() => () => JSNumber.One);
    public static Expression MinusOne = NewLambdaExpression.StaticFieldExpression<JSNumber>(() => () => JSNumber.MinusOne);
    public static Expression Two = NewLambdaExpression.StaticFieldExpression<JSNumber>(() => () => JSNumber.Two);

    public static Expression New(Expression exp)
    {
        if (exp.Type != typeof(double))
            exp = Expression.Convert(exp, typeof(double));

        return Expression.TypeAs(NewLambdaExpression.NewExpression<JSNumber>(() => () => new JSNumber(0), exp), typeof(JSValue));
    }
}
