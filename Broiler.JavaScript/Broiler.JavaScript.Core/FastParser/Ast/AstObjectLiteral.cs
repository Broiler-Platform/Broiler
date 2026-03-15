namespace YantraJS.Core.FastParser;

public class AstObjectLiteral(
    FastToken token,
    FastToken previousToken,
    IFastEnumerable<AstNode> objectProperties) : AstExpression(token, FastNodeType.ObjectLiteral, previousToken)
{
    public readonly IFastEnumerable<AstNode> Properties = objectProperties;
}