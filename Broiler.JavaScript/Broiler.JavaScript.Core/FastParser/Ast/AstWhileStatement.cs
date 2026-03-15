namespace YantraJS.Core.FastParser;

public class AstWhileStatement(FastToken start, FastToken end, AstExpression test, AstStatement statement) : AstStatement(start, FastNodeType.WhileStatement, end)
{
    public readonly AstExpression Test = test;
    public readonly AstStatement Body = statement;
}
