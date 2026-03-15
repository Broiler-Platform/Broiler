namespace YantraJS.Core.FastParser;

public class AstForInStatement(FastToken token, FastToken previousToken, AstNode beginNode, AstExpression target, AstStatement statement) : AstStatement(token, FastNodeType.ForInStatement, previousToken)
{
    public readonly AstNode Init = beginNode;
    public readonly AstExpression Target = target;
    public readonly AstStatement Body = statement;

    public override string ToString() => $"for ({Init} in {Target}) {{ {Body} }}";

}