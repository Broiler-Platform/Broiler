using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Engine;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class LexicalScopeBuilder
{
    public static Expression NewScope(Expression context, Expression fileName, Expression function, int line, int column) =>
        NewLambdaExpression.NewExpression<CallStackItem>(() => () => 
        new CallStackItem(null, "", "", 0, 0), context, fileName, function, Expression.Constant(line), Expression.Constant(column));

    public static Expression Pop(Expression exp, Expression context) => exp.CallExpression<CallStackItem>(() => (x) => x.Pop(null), context);
}
