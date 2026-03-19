using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.Core.LinqExpressions.GeneratorsV2;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSGeneratorFunctionBuilderV2
{
    public static Expression New(Expression @delegate, Expression name, Expression code) =>
        NewLambdaExpression.NewExpression<JSGeneratorFunctionV2>(() => () => new JSGeneratorFunctionV2(null, "", ""), @delegate, name, code);
}
