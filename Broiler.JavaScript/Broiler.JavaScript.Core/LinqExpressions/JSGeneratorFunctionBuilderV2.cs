using YantraJS.Core.LinqExpressions.GeneratorsV2;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LambdaGen;

namespace YantraJS.ExpHelper;

public class JSGeneratorFunctionBuilderV2
{

    public static Expression New(Expression @delegate, Expression name, Expression code) => NewLambdaExpression.NewExpression<JSGeneratorFunctionV2>(() =>
                                                                                                     () => new JSGeneratorFunctionV2((JSGeneratorDelegateV2)null, "", "")
            , @delegate
            , name
            , code);// return Expression.New(_New, @delegate, name, code);
}
