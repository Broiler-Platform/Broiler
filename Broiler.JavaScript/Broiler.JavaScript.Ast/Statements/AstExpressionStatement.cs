using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Statements;


public class AstExpressionStatement : AstStatement
{
    public readonly AstExpression Expression;

    public AstExpressionStatement(FastToken start, FastToken end, AstExpression expression)
        : base(start, FastNodeType.ExpressionStatement, end) => Expression = expression;

    public AstExpressionStatement(AstExpression expression)
        : base(expression.Start, FastNodeType.ExpressionStatement, expression.End) => Expression = expression;
}
