namespace Broiler.JavaScript.Ast;

public class AstSpreadElement(FastToken start, FastToken end, AstExpression element) : AstExpression(start, FastNodeType.SpreadElement, end)
{
    public readonly AstExpression Argument = element;
}
