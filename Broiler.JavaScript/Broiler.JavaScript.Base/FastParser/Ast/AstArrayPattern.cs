using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstArrayPattern(FastToken start, FastToken end, IFastEnumerable<AstExpression> elements) : AstBindingPattern(start, FastNodeType.ArrayPattern, end)
{
    public readonly IFastEnumerable<AstExpression> Elements = elements;

    public override string ToString() => $"[{Elements.Join()}]";
}
