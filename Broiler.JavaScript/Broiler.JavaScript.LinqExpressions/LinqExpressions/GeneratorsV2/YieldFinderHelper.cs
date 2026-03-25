using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;

public static class YieldFinderHelper
{
    private static readonly object yes = new();
    private static readonly object no = new();

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Exp, object> cache = [];

    public static bool HasYield(this Exp expression)
    {
        if (cache.TryGetValue(expression, out var a))
            return ReferenceEquals(a, yes);

        var r = YieldFinder.HasYield(expression);
        cache.Add(expression, r ? yes : no);
        return r;
    }

    public class YieldFinder : YExpressionMapVisitor
    {
        public static bool HasYield(Exp exp)
        {
            var yf = new YieldFinder();
            yf.Visit(exp);
            return yf.hasYield;
        }

        private bool hasYield = false;

        public override Exp VisitIn(Exp exp)
        {
            if (hasYield)
                return exp;

            return base.VisitIn(exp);
        }

        protected override Exp VisitYield(YYieldExpression node)
        {
            hasYield = true;
            return node;
        }

        protected override Exp VisitReturn(YReturnExpression yReturnExpression)
        {
            hasYield = true;
            return yReturnExpression;
        }

        protected override Exp VisitLambda(YLambdaExpression yLambdaExpression) => yLambdaExpression;
    }
}
