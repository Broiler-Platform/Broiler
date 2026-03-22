using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstTemplateExpression(FastToken token, FastToken previousToken, IFastEnumerable<AstExpression> astExpressions) :
    AstExpression(token, FastNodeType.TemplateExpression, previousToken)
{
    public readonly IFastEnumerable<AstExpression> Parts = astExpressions;
}
