using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast;


public class AstSpreadElement(FastToken start, FastToken end, AstExpression element) : AstExpression(start, FastNodeType.SpreadElement, end)
{
    public readonly AstExpression Argument = element;
}
