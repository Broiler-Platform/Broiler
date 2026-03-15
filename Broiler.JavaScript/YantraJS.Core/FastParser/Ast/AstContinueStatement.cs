#nullable enable
namespace YantraJS.Core.FastParser;

public class AstContinueStatement(FastToken token, FastToken previousToken, AstIdentifier? label = null) : AstStatement(token, FastNodeType.ContinueStatement, previousToken)
{
    public readonly AstIdentifier? Label = label;

    public override string ToString()
    {
        if (Label == null)
            return "continue;";
        return $"continue {Label};";
    }
}