
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void SkipNewLines()
    {
        var type = stream.Current.Type;

        while (type == TokenTypes.LineTerminator)
            type = stream.Consume().Type;
    }

    bool Block(out AstBlock node)
    {
        var begin = stream.Current;
        var list = new Sequence<AstStatement>();
        var scope = variableScope.Push(begin, FastNodeType.Block);

        try
        {
            do
            {
                if (stream.CheckAndConsumeAny(TokenTypes.CurlyBracketEnd, TokenTypes.EOF))
                    break;

                if (Statement(out var stmt))
                {
                    // ignore empty expression statement...
                    if (stmt.IsExpressionStatement(out var exp) && exp.Expression.Type == FastNodeType.EmptyExpression)
                        continue;

                    list.Add(stmt);
                    continue;
                }

                if (stream.CheckAndConsumeWithLineTerminator(TokenTypes.SemiColon))
                    continue;

                if (stream.CheckAndConsumeAny(TokenTypes.CurlyBracketEnd, TokenTypes.EOF))
                    break;

                throw stream.Unexpected();
            } while (true);

            node = new AstBlock(begin, PreviousToken, list) { HoistingScope = scope.GetVariables() };
        }
        finally
        {
            scope.Dispose();
        }

        return true;
    }
}
