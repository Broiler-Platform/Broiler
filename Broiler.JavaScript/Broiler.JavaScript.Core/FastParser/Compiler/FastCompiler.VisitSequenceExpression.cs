
using Broiler.JavaScript.ExpressionCompiler.Core;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitSequenceExpression(AstSequenceExpression sequenceExpression)
    {
        var list = new Sequence<Exp>();
        var e = sequenceExpression.Expressions.GetFastEnumerator();
        while (e.MoveNext(out var exp))
        {
            if (exp != null) list.Add(Visit(exp));
        }

        var r = Exp.Block(list);
        return r;
    }
}
