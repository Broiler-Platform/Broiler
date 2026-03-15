#nullable enable
namespace YantraJS.Core.FastParser;

public class AstClassExpression(
    FastToken token,
    FastToken previousToken,
    AstIdentifier? identifier,
    AstExpression? @base,
    IFastEnumerable<AstClassProperty> astClassProperties) : AstExpression(token,  FastNodeType.ClassStatement, previousToken)
{
    public readonly AstIdentifier? Identifier = identifier;
    public readonly AstExpression? Base = @base;
    public readonly IFastEnumerable<AstClassProperty> Members = astClassProperties;

    public override string ToString()
    {
        if(Base != null)
        {
            return $"class {Identifier} extends {Base} {{ {Members.Join("\n\t")} }}";
        }
        if(Identifier == null)
            return $"class {{ {Members.Join("\n\t")} }}";
        return $"class {Identifier} {{ {Members.Join("\n\t")} }}";
    }
}