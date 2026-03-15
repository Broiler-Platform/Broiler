using System.Collections.Generic;
using YantraJS.Expressions;

namespace YantraJS.Core.LinqExpressions.GeneratorsV2;

internal class ReplaceParameters(Dictionary<YExpression, YExpression> replacers) : YExpressionMapVisitor
{
    public override YExpression VisitIn(YExpression exp)
    {
        if (exp == null)
        {
            return null;
        }
        if(replacers.TryGetValue(exp,out var replaced))
        {
            exp = replaced;
        }
        return base.VisitIn(exp);
    }


}
