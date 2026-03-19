#nullable enable
using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;
using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstArrayExpression(FastToken start, FastToken end, IFastEnumerable<AstExpression> nodes) : AstExpression(start, FastNodeType.ArrayExpression, end)
{
    public readonly IFastEnumerable<AstExpression> Elements = nodes;

    public override string ToString() => $"[{Elements.Join()}]";
}