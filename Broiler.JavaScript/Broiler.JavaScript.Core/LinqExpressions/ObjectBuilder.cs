using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;


public class ObjectBuilder
{
    public static Expression ToString(Expression value) => value.CallExpression<object, string>(() => (x) => x.ToString(), value);
}
