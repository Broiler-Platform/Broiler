using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Statements;


public class AstImportStatement(FastToken token, AstIdentifier? defaultIdentifier, AstIdentifier? all, IFastEnumerable<(StringSpan, StringSpan)>? members, AstLiteral source, 
    IFastEnumerable<(StringSpan, AstLiteral)>? attributes = null) : AstStatement(token, FastNodeType.ImportStatement, source.End)
{
    public readonly AstIdentifier? Default = defaultIdentifier;
    public readonly AstIdentifier? All = all;
    public readonly IFastEnumerable<(StringSpan name, StringSpan asName)>? Members = members;
    public readonly AstLiteral Source = source;

    public readonly IFastEnumerable<(StringSpan key, AstLiteral value)>? Attributes = attributes;
}
