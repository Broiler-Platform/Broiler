using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstTaggedTemplateExpression(AstExpression tag, IFastEnumerable<AstExpression> arguments) : 
    AstExpression(arguments.FirstOrDefault().Start, FastNodeType.TaggedTemplateExpression, arguments.LastOrDefault().End)
{
    public readonly AstExpression Tag = tag;
    public readonly IFastEnumerable<AstExpression> Arguments = arguments;
}
