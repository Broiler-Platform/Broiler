using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LambdaGen;

namespace YantraJS.ExpHelper;

public class JSBooleanBuilder 
{
    // static Type type = typeof(JSBoolean);

    public static Expression True =
        NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSBoolean.True);
        // Expression.TypeAs( Expression.Field(null, type.GetField(nameof(JSBoolean.True))), typeof(JSValue));

    public static Expression False =
        NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSBoolean.False);
    // Expression.TypeAs( Expression.Field(null, type.GetField(nameof(JSBoolean.False))), typeof(JSValue));

    //private static FieldInfo _Value =
    //    type.InternalField(nameof(Core.JSBoolean._value));

    //public static Expression Value(Expression target)
    //{
    //    return Expression.Field(target, _Value);
    //}

    public static Expression NewFromCLRBoolean(Expression target) => Expression.Condition(target, JSBooleanBuilder.True, JSBooleanBuilder.False);


    public static Expression Not(Expression value) => Expression.Condition(
            JSValueBuilder.BooleanValue(value),
            JSBooleanBuilder.False,
            JSBooleanBuilder.True
            );
}
