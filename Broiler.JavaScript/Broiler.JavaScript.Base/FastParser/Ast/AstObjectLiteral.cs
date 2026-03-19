using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstObjectLiteral(FastToken token, FastToken previousToken, IFastEnumerable<AstNode> objectProperties) : AstExpression(token, FastNodeType.ObjectLiteral, previousToken)
{
    public readonly IFastEnumerable<AstNode> Properties = objectProperties;
}
