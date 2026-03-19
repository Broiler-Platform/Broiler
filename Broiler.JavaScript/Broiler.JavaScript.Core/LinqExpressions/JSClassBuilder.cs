using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Expressions;
using Broiler.JavaScript.Core.Core.Class;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSClassBuilder
{
    public static YElementInit AddConstructor(Expression exp) => Expression.ElementInit(Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod<JSClass>(() => (x) => x.AddConstructor(null)), exp);

    public static YNewExpression New(Expression constructor, Expression super, string name, string code = "") =>
        NewLambdaExpression.NewExpression<JSClass>(() => () => new JSClass(null, null, null, null),
            constructor ?? Expression.Null, super ?? Expression.Null, Expression.Constant(name), Expression.Constant(code));
}
