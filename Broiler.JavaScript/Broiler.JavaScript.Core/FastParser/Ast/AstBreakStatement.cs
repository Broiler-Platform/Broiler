#nullable enable
using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstBreakStatement(FastToken token, FastToken previousToken, AstIdentifier? label = null) : AstStatement(token, FastNodeType.BreakStatement, previousToken)
{
    public readonly AstIdentifier? Label = label;

    public override string ToString() => Label != null ? $"break {Label};" : $"break;";
}