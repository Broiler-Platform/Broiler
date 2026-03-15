namespace YantraJS.Core.FastParser;

public class AstTemplateExpression(FastToken token, FastToken previousToken, IFastEnumerable<AstExpression> astExpressions) : AstExpression(token, FastNodeType.TemplateExpression, previousToken)
{
    public readonly IFastEnumerable<AstExpression> Parts = astExpressions;
}