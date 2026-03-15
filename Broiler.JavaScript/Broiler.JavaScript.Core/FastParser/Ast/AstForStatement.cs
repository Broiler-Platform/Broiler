namespace YantraJS.Core.FastParser;

public class AstForStatement(FastToken token, FastToken previousToken, AstNode beginNode, AstExpression test, AstExpression preTest, AstStatement statement) : AstStatement(token, FastNodeType.ForStatement, previousToken)
{
    public readonly AstNode Init = beginNode;
    public readonly AstExpression Test = test;
    public readonly AstExpression Update = preTest;
    public readonly AstStatement Body = statement;

    public override string ToString() => $"for ({Init};{Update};{Test}) {{ {Body} }}";
}