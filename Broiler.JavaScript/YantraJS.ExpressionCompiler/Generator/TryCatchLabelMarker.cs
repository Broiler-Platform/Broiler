using YantraJS.Core;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public class TryCatchLabelMarker(ILTryBlock tryBlock, LabelInfo labels) : YExpressionMapVisitor
{
    public static void Collect(YTryCatchFinallyExpression body, ILTryBlock tryBlock, LabelInfo labels)
    {
        TryCatchLabelMarker t = new(tryBlock, labels);
        t.Visit(body.Try);
        if (body.Catch != null)
            t.Visit(body.Catch.Body);
        if (body.Finally != null)
            t.Visit(body.Finally);
    }

    protected override YExpression VisitLabel(YLabelExpression yLabelExpression)
    {
        labels.Create(yLabelExpression.Target, tryBlock, false);
        return base.VisitLabel(yLabelExpression);
    }

    //protected override YExpression VisitReturn(YReturnExpression yReturnExpression)
    //{
    //    labels.Create(yReturnExpression.Target, tryBlock, false);
    //    return base.VisitReturn(yReturnExpression);
    //}

    protected override YExpression VisitLoop(YLoopExpression yLoopExpression)
    {
        labels.Create(yLoopExpression.Break, tryBlock, false);
        labels.Create(yLoopExpression.Continue, tryBlock, false);
        return base.VisitLoop(yLoopExpression);
    }

    protected override YExpression VisitTryCatchFinally(YTryCatchFinallyExpression tryCatchFinallyExpression) => tryCatchFinallyExpression;

    protected override YExpression VisitLambda(YLambdaExpression yLambdaExpression) => yLambdaExpression;

    //protected override YExpression VisitRelay(YRelayExpression relayExpression)
    //{
    //    return relayExpression;
    //}
}
