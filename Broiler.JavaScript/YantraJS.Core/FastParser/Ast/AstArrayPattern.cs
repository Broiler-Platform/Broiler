namespace YantraJS.Core.FastParser;

public class AstArrayPattern(FastToken start, FastToken end, IFastEnumerable<AstExpression> elements) : AstBindingPattern(start, FastNodeType.ArrayPattern, end)
{
    public readonly IFastEnumerable<AstExpression> Elements = elements;

    public override string ToString() => $"[{Elements.Join()}]";
}
