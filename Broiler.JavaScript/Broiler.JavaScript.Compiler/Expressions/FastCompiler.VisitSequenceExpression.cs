using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitSequenceExpression(AstSequenceExpression sequenceExpression)
    {
        var list = new Sequence<YExpression>();
        var e = sequenceExpression.Expressions.GetFastEnumerator();
        while (e.MoveNext(out var exp))
        {
            if (exp != null) list.Add(Visit(exp));
        }

        var r = YExpression.Block(list);
        return r;
    }
}
