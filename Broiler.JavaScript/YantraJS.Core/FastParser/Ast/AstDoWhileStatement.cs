namespace YantraJS.Core.FastParser;

public class AstDoWhileStatement(
    FastToken start,
    FastToken end,
    AstExpression test,
    AstStatement statement) : AstStatement(start, FastNodeType.DoWhileStatement, end)
{
    public readonly AstExpression Test = test;
    public readonly AstStatement Body = statement;

    public override string ToString() => @$"do {{
    {Body} 
}} while ({Test})";
}
