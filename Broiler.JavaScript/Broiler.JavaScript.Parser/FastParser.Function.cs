using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    bool Function(out AstStatement statement, bool isAsync = false)
    {
        if (!FunctionExpression(out var expression, isAsync, isStatement: true))
        {
            statement = default;
            return false;
        }

        statement = new AstExpressionStatement(expression);
        return true;
    }

    bool FunctionExpression(out AstExpression node, bool isAsync = false, bool isStatement = false)
    {
        bool isRootAsync = this.isAsync;
        var begin = stream.Current;
        node = default;
        stream.Consume();
        var generator = false;

        if (stream.CheckAndConsume(TokenTypes.Multiply))
            generator = true;

        if (Identitifer(out var id))
        {
            // BROILER-PATCH: For function declarations, add name to parent scope (hoisted).
            // For function expressions, do NOT add to parent scope (ES3 §13).
            if (isStatement)
                variableScope.Top.AddVariable(id.Start, id.Name, FastVariableKind.Let);
        }

        stream.Expect(TokenTypes.BracketStart);
        var scope = variableScope.Push(begin, FastNodeType.FunctionExpression);

        if (!Parameters(out var declarators, TokenTypes.BracketEnd, false, FastVariableKind.Let))
            throw stream.Unexpected();

        if (!stream.CheckAndConsume(TokenTypes.CurlyBracketStart))
            throw stream.Unexpected();

        try
        {
            if (!Block(out var body))
                throw stream.Unexpected();

            node = new AstFunctionExpression(begin, PreviousToken, false, isAsync, generator, id, declarators, body, isStatement);
        }
        finally
        {
            scope.Dispose();
        }

        this.isAsync = isRootAsync;
        return true;
    }
}
