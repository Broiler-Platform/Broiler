namespace YantraJS.Core.FastParser;

public class AstNewExpression(FastToken begin,
    AstExpression node,
    IFastEnumerable<AstExpression> arguments) : AstExpression(begin, FastNodeType.NewExpression, node.End)
{
    public readonly AstExpression Callee = node;
    public readonly IFastEnumerable<AstExpression> Arguments = arguments;

    public override string ToString() => $"new {Callee}({Arguments.Join()})";
}