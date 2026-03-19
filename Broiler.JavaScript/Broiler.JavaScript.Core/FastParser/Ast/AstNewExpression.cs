using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstNewExpression(FastToken begin, AstExpression node, IFastEnumerable<AstExpression> arguments) : AstExpression(begin, FastNodeType.NewExpression, node.End)
{
    public readonly AstExpression Callee = node;
    public readonly IFastEnumerable<AstExpression> Arguments = arguments;

    public override string ToString() => $"new {Callee}({Arguments.Join()})";
}