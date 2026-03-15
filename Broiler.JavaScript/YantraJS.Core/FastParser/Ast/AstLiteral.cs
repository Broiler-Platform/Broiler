namespace YantraJS.Core.FastParser;

public class AstLiteral(TokenTypes tokenType, FastToken token) : AstExpression(token, FastNodeType.Literal, token)
{
    public readonly TokenTypes TokenType = tokenType;

    public double NumericValue => Start.Number;

    public string StringValue => Start.CookedText ?? Start.Span.Value;

    public (string Pattern, string Flags) Regex => (Start.CookedText, Start.Flags);

    public override string ToString() => TokenType.ToString();
}

public class AstSuper(FastToken token) : AstExpression(token, FastNodeType.Super, token)
{
    public readonly TokenTypes TokenType;

    public override string ToString() => "super";
}