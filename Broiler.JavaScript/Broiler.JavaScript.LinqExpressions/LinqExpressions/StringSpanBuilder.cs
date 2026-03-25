using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public static class StringSpanBuilder
{
    public static Expression New(Expression code, int start, int v) =>
        NewLambdaExpression.NewExpression<StringSpan>(() => () => new StringSpan("", 0, 0), code, Expression.Constant(start), Expression.Constant(v));

    public static Expression New(in StringSpan code) =>
        NewLambdaExpression.NewExpression<StringSpan>(() => () => new StringSpan("", 0, 0), Expression.Constant(code.Source), Expression.Constant(code.Offset), Expression.Constant(code.Length));

    public static readonly Expression Empty = NewLambdaExpression.StaticFieldExpression<StringSpan>(() => () => StringSpan.Empty);
}
