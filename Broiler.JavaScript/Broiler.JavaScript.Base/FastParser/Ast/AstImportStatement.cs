#nullable enable
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;
using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstImportStatement(FastToken token, AstIdentifier? defaultIdentifier, AstIdentifier? all, IFastEnumerable<(StringSpan, StringSpan)>? members, AstLiteral source, 
    IFastEnumerable<(StringSpan, AstLiteral)>? attributes = null) : AstStatement(token, FastNodeType.ImportStatement, source.End)
{
    public readonly AstIdentifier? Default = defaultIdentifier;
    public readonly AstIdentifier? All = all;
    public readonly IFastEnumerable<(StringSpan name, StringSpan asName)>? Members = members;
    public readonly AstLiteral Source = source;

    /// <summary>
    /// Import attributes from <c>with { key: "value" }</c> clause (ES2025 §2.3).
    /// Each tuple is (attributeKey, attributeValue).
    /// </summary>
    public readonly IFastEnumerable<(StringSpan key, AstLiteral value)>? Attributes = attributes;
}
