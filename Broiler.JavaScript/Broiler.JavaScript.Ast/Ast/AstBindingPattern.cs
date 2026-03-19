namespace Broiler.JavaScript.Ast;

public class AstBindingPattern(FastToken start, FastNodeType type, FastToken end) : AstExpression(start, type, end, true)
{
}
