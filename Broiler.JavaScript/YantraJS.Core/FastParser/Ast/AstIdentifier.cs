namespace YantraJS.Core.FastParser;

public class AstIdentifier : AstExpression
{
    public readonly StringSpan Name;

    public AstIdentifier(FastToken identifier) : base(identifier, FastNodeType.Identifier, identifier) => Name = identifier.Span;

    public AstIdentifier(FastToken token, string id) : base(token, FastNodeType.Identifier, token) => Name = id;


    public override string ToString() => Name.Value;

}
