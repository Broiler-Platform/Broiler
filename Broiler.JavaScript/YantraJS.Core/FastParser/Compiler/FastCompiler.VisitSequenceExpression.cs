using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

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
        // list.Clear();
        return r;
    }
}
