using YantraJS.Core;
using YantraJS.Core.LambdaGen;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.ExpHelper;

public class JSUndefinedBuilder
{
    public static Expression Value =
        NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSUndefined.Value);
        //Expression.Field(null,
        //    typeof(JSUndefined).GetField(nameof(Core.JSUndefined.Value)));
}
