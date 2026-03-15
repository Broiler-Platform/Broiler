using YantraJS.Core;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitLambda(YLambdaExpression yLambdaExpression)
    {

        var closureRepository = yLambdaExpression.GetClosureRepository();
        var captures = closureRepository.Inputs.AsSequence<YExpression>();
        yLambdaExpression.SetupAsClosure();

        return Visit(methodBuilder.Relay(This, captures, yLambdaExpression));
    }
}
