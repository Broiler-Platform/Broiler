#nullable enable
namespace YantraJS.Core.FastParser;

public class AstArrayExpression(FastToken start, FastToken end, IFastEnumerable<AstExpression> nodes) : AstExpression(start, FastNodeType.ArrayExpression, end)
{
    public readonly IFastEnumerable<AstExpression> Elements = nodes;

    public override string ToString() => $"[{Elements.Join()}]";
}