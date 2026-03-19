namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstExpressionStatement : AstStatement
{
    public readonly AstExpression Expression;

    public AstExpressionStatement(FastToken start, FastToken end, AstExpression expression)
        : base(start, FastNodeType.ExpressionStatement, end) => Expression = expression;

    public AstExpressionStatement(AstExpression expression)
        : base(expression.Start, FastNodeType.ExpressionStatement, expression.End) => Expression = expression;
}
