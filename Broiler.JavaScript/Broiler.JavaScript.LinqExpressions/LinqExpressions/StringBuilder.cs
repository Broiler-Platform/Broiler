using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.LinqExpressions.LambdaGen;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class ClrStringBuilder
{
    public static Expression Equal(Expression left, Expression right) => left.CallExpression<string, bool>(() => (x) => x.Equals(""), right);
    public static Expression NotEqual(Expression left, Expression right) => Expression.Not(Equal(left, right));
    public static Expression Concat(Expression left, Expression right) => NewLambdaExpression.StaticCallExpression(() => () => string.Concat("", ""), left, right);
}
