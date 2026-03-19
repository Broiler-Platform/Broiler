namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstEmptyExpression(FastToken start, bool isBinding = false) : AstExpression(start, FastNodeType.EmptyExpression, start, isBinding)
{
    public override string ToString() => "<<Empty>>";
}
