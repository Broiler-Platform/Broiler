#nullable enable
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;

partial class FastParser
{
    bool Import(FastToken token, out AstStatement statement)
    {
        stream.Consume();

        AstIdentifier id;

        if (stream.CheckAndConsume(TokenTypes.Multiply))
        {
            stream.ExpectContextualKeyword(FastKeywords.@as);

            if (!Identitifer(out id))
                throw stream.Unexpected();

            stream.ExpectContextualKeyword(FastKeywords.from);

            var literal = ExpectStringLiteral();
            var attrs = ImportAttributes();

            isAsync = true;
            statement = new AstImportStatement(token, null, id, null, literal, attrs);

            return true;

        }

        AstIdentifier? all = null;
        IFastEnumerable<(StringSpan, StringSpan)>? names = null;

        if (Identitifer(out id))
        {
            if (stream.CheckAndConsume(TokenTypes.Comma))
            {
                if (stream.CheckAndConsume(TokenTypes.Multiply))
                {
                    stream.ExpectContextualKeyword(FastKeywords.@as);

                    if (!Identitifer(out all))
                        throw stream.Unexpected();
                }
                else if (ImportNames(out var n))
                {
                    names = n;
                }
                else throw stream.Unexpected();
            }

            stream.ExpectContextualKeyword(FastKeywords.from);

            var literal = ExpectStringLiteral();
            var attrs = ImportAttributes();

            isAsync = true;
            statement = new AstImportStatement(token, id, all, names, literal, attrs);

            return true;
        }

        if (ImportNames(out names))
        {
            if (stream.CheckAndConsume(TokenTypes.Comma))
            {
                if (!Identitifer(out id))
                    throw stream.Unexpected();
            }

            stream.ExpectContextualKeyword(FastKeywords.from);

            var literal = ExpectStringLiteral();
            var attrs = ImportAttributes();

            isAsync = true;
            statement = new AstImportStatement(token, id, all, names, literal, attrs);

            return true;
        }

        throw stream.Unexpected();

        bool ImportNames(out IFastEnumerable<(StringSpan, StringSpan)>? names)
        {
            if (!stream.CheckAndConsume(TokenTypes.CurlyBracketStart))
            {
                names = null;
                return false;
            }

            var list = new Sequence<(StringSpan, StringSpan)>();

            while (!stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
            {
                if (!Identitifer(out var id))
                    throw stream.Unexpected();

                if (stream.CheckAndConsumeContextualKeyword(FastKeywords.@as))
                {
                    if (!Identitifer(out var asName))
                        throw stream.Unexpected();
                    list.Add((id.Name, asName.Name));
                }
                else
                {
                    list.Add((id.Name, id.Name));
                }

                if (stream.CheckAndConsume(TokenTypes.Comma))
                    continue;

                if (stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
                    break;

                throw stream.Unexpected();
            }

            names = list;
            return true;
        }
    }

    /// <summary>
    /// Parse optional import attributes: <c>with { key: "value", ... }</c>
    /// (ES2025 §2.3 Import Attributes).
    /// </summary>
    IFastEnumerable<(StringSpan, AstLiteral)>? ImportAttributes()
    {
        // The `with` keyword is a reserved keyword, so use CheckAndConsume(FastKeywords)
        if (!stream.CheckAndConsume(FastKeywords.@with))
            return null;

        if (!stream.CheckAndConsume(TokenTypes.CurlyBracketStart))
            throw stream.Unexpected();

        var list = new Sequence<(StringSpan, AstLiteral)>();

        while (!stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
        {
            // Attribute key can be an identifier or a string literal
            StringSpan key;
            if (Identitifer(out var keyId))
            {
                key = keyId.Name;
            }
            else
            {
                throw stream.Unexpected();
            }

            // Expect colon separator
            if (!stream.CheckAndConsume(TokenTypes.Colon))
                throw stream.Unexpected();

            // Attribute value must be a string literal
            var value = ExpectStringLiteral();
            list.Add((key, value));

            if (stream.CheckAndConsume(TokenTypes.Comma))
                continue;

            if (stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
                break;

            throw stream.Unexpected();
        }

        return list;
    }
}
