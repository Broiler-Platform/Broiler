using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstSequenceExpression : AstExpression
{
    public readonly IFastEnumerable<AstExpression> Expressions;

    public AstSequenceExpression(FastToken start, FastToken end, IFastEnumerable<AstExpression> expressions) : base(start, FastNodeType.SequenceExpression, end) =>
        Expressions = expressions;

    public AstSequenceExpression(IFastEnumerable<AstExpression> expressions) :
        base(expressions.FirstOrDefault().Start, FastNodeType.SequenceExpression, expressions.LastOrDefault().End) => Expressions = expressions;

    public override string ToString() => Expressions.Join();
}
