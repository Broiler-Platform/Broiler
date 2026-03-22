using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    /// <summary>
    /// Expression Sequence represents a comma separated expressions
    /// terminated by new line or semi colon
    /// </summary>
    /// <param name="expressions"></param>
    /// <param name="endWith"></param>
    /// <param name="allowEmpty"></param>
    /// <returns></returns>
    bool ExpressionSequence(out AstExpression expressions, TokenTypes endWith = TokenTypes.BracketEnd, bool allowEmpty = false)
    {
        var begin = stream.Current;
        var nodes = new Sequence<AstExpression>();

        do
        {
            if (allowEmpty && stream.Current.Type == TokenTypes.CurlyBracketEnd)
                break;

            if (allowEmpty && stream.CheckAndConsumeAny(endWith, TokenTypes.EOF, TokenTypes.SemiColon))
                break;

            allowEmpty = false;

            if (Expression(out var node))
                nodes.Add(node);

            if (stream.CheckAndConsume(TokenTypes.Comma))
                continue;

            if (stream.CheckAndConsumeAny(endWith, TokenTypes.EOF, TokenTypes.SemiColon))
                break;

            if (stream.Current.Type == TokenTypes.CurlyBracketEnd)
                break;

            if (stream.LineTerminator())
                break;

            break;
        } while (true);

        expressions = nodes.Count switch
        {
            0 => new AstEmptyExpression(begin),
            1 => nodes[0],
            _ => new AstSequenceExpression(begin, PreviousToken, nodes),
        };

        return true;
    }

    bool WhileStatement(out AstStatement node)
    {
        var begin = stream.Current;

        stream.Consume();
        stream.Expect(TokenTypes.BracketStart);

        ExpressionSequence(out var test);

        if (!NonDeclarativeStatement(out var statement))
            throw stream.Unexpected();

        node = new AstWhileStatement(begin, PreviousToken, test, statement);
        return true;
    }
}
