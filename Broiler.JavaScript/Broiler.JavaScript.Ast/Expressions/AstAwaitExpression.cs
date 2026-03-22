using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstAwaitExpression(FastToken token, FastToken previousToken, AstExpression target) : AstExpression(token, FastNodeType.AwaitExpression, previousToken)
{
    public readonly AstExpression Argument = target;
}
