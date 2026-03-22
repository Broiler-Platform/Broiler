using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast;


public class AstObjectLiteral(FastToken token, FastToken previousToken, IFastEnumerable<AstNode> objectProperties) : AstExpression(token, FastNodeType.ObjectLiteral, previousToken)
{
    public readonly IFastEnumerable<AstNode> Properties = objectProperties;
}
