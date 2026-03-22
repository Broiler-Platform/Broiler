
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    bool Class(out AstStatement statement)
    {
        statement = default;

        if (ClassExpression(out var node, isStatement: true))
        {
            statement = new AstExpressionStatement(node);
            return true;
        }

        return false;
    }

    bool ClassExpression(out AstExpression statement, bool isStatement = false)
    {
        var begin = stream.Current;
        statement = default;

        var next = stream.Consume();
        AstIdentifier identifier = null;
        AstExpression @base = null;

        if (next.Type != TokenTypes.CurlyBracketStart)
        {
            if (next.Keyword != FastKeywords.extends)
            {
                if (!Identitifer(out identifier))
                    throw stream.Unexpected();
            }

            if (stream.CheckAndConsume(FastKeywords.extends))
            {
                if (!Expression(out @base))
                    throw stream.Unexpected();
            }
        }

        stream.Expect(TokenTypes.CurlyBracketStart);

        var nodes = new Sequence<AstClassProperty>();
        while (!stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
        {
            if (ObjectProperty(out var property, true, isClass: true))
                nodes.Add(property);

            stream.CheckAndConsumeWithLineTerminator(TokenTypes.SemiColon);
        }

        if (identifier != null)
            variableScope.Top.AddVariable(identifier.Start, identifier.Name, isStatement ? FastVariableKind.Let : FastVariableKind.Var, throwError: false);

        statement = new AstClassExpression(begin, PreviousToken, identifier, @base, nodes);

        return true;
    }
}
