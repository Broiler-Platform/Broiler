using System.Collections.Generic;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;

internal class ReplaceParameters(Dictionary<YExpression, YExpression> replacers) : YExpressionMapVisitor
{
    public override YExpression VisitIn(YExpression exp)
    {
        if (exp == null)
            return null;

        if(replacers.TryGetValue(exp,out var replaced))
            exp = replaced;

        return base.VisitIn(exp);
    }


}
