namespace YantraJS.Core.FastParser;

public class AstAwaitExpression(FastToken token, FastToken previousToken, AstExpression target) : AstExpression(token, FastNodeType.AwaitExpression, previousToken)
{
    public readonly AstExpression Argument = target;
}