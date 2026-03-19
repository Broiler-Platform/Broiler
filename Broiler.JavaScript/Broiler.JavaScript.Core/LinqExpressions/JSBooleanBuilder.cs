using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.Core.Core.Boolean;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSBooleanBuilder
{
    public static Expression True = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSBoolean.True);
    public static Expression False = NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSBoolean.False);

    public static Expression NewFromCLRBoolean(Expression target) => Expression.Condition(target, True, False);

    public static Expression Not(Expression value) => Expression.Condition(JSValueBuilder.BooleanValue(value), False, True);
}
