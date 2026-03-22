using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstEmptyExpression(FastToken start, bool isBinding = false) : AstExpression(start, FastNodeType.EmptyExpression, start, isBinding)
{
    public override string ToString() => "<<Empty>>";
}
