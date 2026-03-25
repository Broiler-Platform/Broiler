using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSBooleanBuilder
{
    public static Expression True = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSValue.BooleanTrue);
    public static Expression False = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSValue.BooleanFalse);

    public static Expression NewFromCLRBoolean(Expression target) => Expression.Condition(target, True, False);

    public static Expression Not(Expression value) => Expression.Condition(JSValueBuilder.BooleanValue(value), False, True);
}
