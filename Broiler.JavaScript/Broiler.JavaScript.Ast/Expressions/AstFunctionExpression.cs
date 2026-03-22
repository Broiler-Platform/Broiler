using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstFunctionExpression(FastToken token, FastToken previousToken, bool isArrow, bool isAsync, bool generator, AstIdentifier? id,
    IFastEnumerable<VariableDeclarator> declarators, AstStatement body, bool isStatement = false) : AstExpression(token, FastNodeType.FunctionExpression, previousToken)
{
    public bool Async = isAsync;
    public bool Generator = generator;
    public readonly AstIdentifier? Id = id;
    public readonly IFastEnumerable<VariableDeclarator> Params = declarators;
    public readonly AstStatement Body = body;
    public readonly bool IsArrowFunction = isArrow;
    // BROILER-PATCH: Track whether this is a function declaration vs expression (ES3 §13)
    public new readonly bool IsStatement = isStatement;

    public override string ToString()
    {
        if (IsArrowFunction)
        {
            if (Async)
            {
                if (Generator)
                {
                    if (Id != null)
                        return $"async *{Id}({Params.Join()}) => {Body}";

                    return $"async *({Params.Join()}) => {Body}";
                }

                if (Id != null)
                    return $"async {Id}({Params.Join()}) => {Body}";

                return $"async ({Params.Join()}) => {Body}";
            }

            if (Generator)
            {
                if (Id != null)
                    return $"*{Id}({Params.Join()}) => {Body}";

                return $"*({Params.Join()}) => {Body}";
            }

            if (Id != null)
                return $"{Id}({Params.Join()}) => {Body}";
            
            return $"({Params.Join()}) => {Body}";

        }

        if (Async)
        {
            if (Generator)
            {
                if (Id != null)
                    return $"async function *{Id}({Params.Join()}) {Body}";

                return $"async function *({Params.Join()}) {Body}";
            }

            if (Id != null)
                return $"async function {Id}({Params.Join()}) {Body}";
            
            return $"async function ({Params.Join()}) {Body}";
        }

        if (Generator)
        {
            if (Id != null)
                return $"function *{Id}({Params.Join()}) {Body}";

            return $"function *({Params.Join()}) {Body}";
        }

        if (Id != null)
            return $"function {Id}({Params.Join()}) {Body}";

        return $"function ({Params.Join()}) {Body}";
    }
}
