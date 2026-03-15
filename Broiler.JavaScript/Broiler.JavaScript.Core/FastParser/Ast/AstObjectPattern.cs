namespace YantraJS.Core.FastParser;

public class AstObjectPattern(
    FastToken start,
    FastToken end,
    IFastEnumerable<ObjectProperty> properties) : AstBindingPattern(start, FastNodeType.ObjectPattern, end)
{
    public readonly IFastEnumerable<ObjectProperty> Properties = properties;
}
