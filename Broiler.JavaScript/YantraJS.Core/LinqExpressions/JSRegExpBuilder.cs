using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LambdaGen;

namespace YantraJS.ExpHelper;

public class JSRegExpBuilder
{
    // private static ConstructorInfo _New = typeof(JSRegExp).Constructor(typeof(string), typeof(string));

    public static Expression New(Expression exp, Expression exp2) => Expression.TypeAs(
                    NewLambdaExpression.NewExpression<JSRegExp>(() => () => new JSRegExp("", "")
                    , exp
                    , exp2)
                , typeof(JSValue));// return Expression.TypeAs( Expression.New(_New, exp, exp2), typeof(JSValue));

}
