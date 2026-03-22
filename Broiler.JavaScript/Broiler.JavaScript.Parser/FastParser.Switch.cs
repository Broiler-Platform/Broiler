
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    bool Switch(out AstStatement node)
    {
        var begin = stream.Current;
        stream.Consume();
        node = null;

        stream.Expect(TokenTypes.BracketStart);

        if (!Expression(out var target))
            throw stream.Unexpected();

        stream.Expect(TokenTypes.BracketEnd);

        stream.Expect(TokenTypes.CurlyBracketStart);

        var nodes = new Sequence<Case>();
        var statements = new Sequence<AstStatement>();
        AstExpression test = null;
        bool hasDefault = false;

        while (!stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
        {
            if (stream.CheckAndConsume(FastKeywords.@case))
            {
                if (test != null)
                {
                    nodes.Add(new Case(test, statements));
                    statements = [];
                }

                if (!Expression(out test))
                    throw stream.Unexpected();

                stream.Expect(TokenTypes.Colon);
            }
            else if (stream.CheckAndConsume(FastKeywords.@default))
            {
                stream.Expect(TokenTypes.Colon);

                if (test != null)
                {
                    nodes.Add(new Case(test, statements));
                    statements = [];
                }

                test = null;
                hasDefault = true;
            }
            else if (Statement(out var stmt))
                statements.Add(stmt);
        }

        if (test != null || hasDefault)
            nodes.Add(new Case(test, statements));

        node = new AstSwitchStatement(begin, PreviousToken, target, nodes);
        return true;
    }
}
