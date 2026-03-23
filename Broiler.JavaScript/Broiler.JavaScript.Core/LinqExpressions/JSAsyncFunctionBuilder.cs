using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSAsyncFunctionBuilder
{
    public static YExpression Create(YExpression fx) => NewLambdaExpression.StaticCallExpression<JSValue>(() => () => JSAsyncFunction.Create(null), fx);
}
