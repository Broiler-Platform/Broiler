namespace YantraJS.Core.FastParser;

public class AstThrowStatement(FastToken token, FastToken previousToken, AstExpression target) : AstStatement(token, FastNodeType.ThrowStatement, previousToken)
{
    public readonly AstExpression Argument = target;
}