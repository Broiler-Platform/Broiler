using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.LambdaGen;
using YantraJS.Core;
using YantraJS.Expressions;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSAsyncFunctionBuilder
{
    public static YExpression Create(YExpression fx) => NewLambdaExpression.StaticCallExpression<JSFunction>(() => () => JSAsyncFunction.Create(null), fx);
}
