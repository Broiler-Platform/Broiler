namespace YantraJS.Core.FastParser;

public class AstMeta(AstIdentifier id, AstIdentifier property) : AstExpression(id.Start, FastNodeType.Meta, property.End)
{
    public readonly AstIdentifier Identifier = id;
    public readonly AstIdentifier Property = property;
}