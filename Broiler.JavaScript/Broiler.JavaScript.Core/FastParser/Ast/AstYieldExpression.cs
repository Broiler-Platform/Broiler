namespace YantraJS.Core.FastParser;

public class AstYieldExpression(FastToken token, FastToken previousToken, AstExpression target, bool @delegate = false) : AstExpression(token, FastNodeType.YieldExpression, previousToken)
{
    public readonly AstExpression Argument = target;
    public readonly bool Delegate = @delegate;
}