using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LambdaGen;

namespace YantraJS.ExpHelper;

public class JSNullBuilder
{

    public static Expression Value =
         // Expression.TypeAs(
             NewLambdaExpression.StaticFieldExpression<JSValue>(() => () => JSNull.Value)
             //, Expression.Field(
             //       null, 
             //       typeof(JSNull)
             //           .GetField(nameof(JSNull.Value))), 
             // typeof(JSValue))
             ;
}
