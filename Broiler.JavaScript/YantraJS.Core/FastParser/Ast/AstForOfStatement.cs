namespace YantraJS.Core.FastParser;

public class AstForOfStatement(FastToken token, FastToken previousToken, AstNode beginNode, AstExpression target, AstStatement statement) : AstStatement(token, FastNodeType.ForOfStatement, previousToken)
{
    public readonly AstNode Init = beginNode;
    public readonly AstExpression Target = target;
    public readonly AstStatement Body = statement;

    public override string ToString() => $"for ({Init} of {Target}) {{ {Body} }}";

}