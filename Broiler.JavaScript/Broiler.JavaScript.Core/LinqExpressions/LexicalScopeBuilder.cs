using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class LexicalScopeBuilder
{
    public static Expression NewScope(Expression context, Expression fileName, Expression function, int line, int column) =>
        NewLambdaExpression.NewExpression<CallStackItem>(() => () => 
        new CallStackItem(null, "", "", 0, 0), context, fileName, function, Expression.Constant(line), Expression.Constant(column));

    public static Expression Pop(Expression exp, Expression context) => exp.CallExpression<CallStackItem>(() => (x) => x.Pop(null), context);
}
