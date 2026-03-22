using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstArrayExpression(FastToken start, FastToken end, IFastEnumerable<AstExpression> nodes) : AstExpression(start, FastNodeType.ArrayExpression, end)
{
    public readonly IFastEnumerable<AstExpression> Elements = nodes;

    public override string ToString() => $"[{Elements.Join()}]";
}
