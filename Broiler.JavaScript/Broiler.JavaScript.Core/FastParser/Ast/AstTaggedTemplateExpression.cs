using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstTaggedTemplateExpression(AstExpression tag, IFastEnumerable<AstExpression> arguments) : 
    AstExpression(arguments.FirstOrDefault().Start, FastNodeType.TaggedTemplateExpression, arguments.LastOrDefault().End)
{
    public readonly AstExpression Tag = tag;
    public readonly IFastEnumerable<AstExpression> Arguments = arguments;
}