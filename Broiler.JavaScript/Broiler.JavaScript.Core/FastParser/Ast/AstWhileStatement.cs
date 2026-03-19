namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstWhileStatement(FastToken start, FastToken end, AstExpression test, AstStatement statement) : AstStatement(start, FastNodeType.WhileStatement, end)
{
    public readonly AstExpression Test = test;
    public readonly AstStatement Body = statement;
}
