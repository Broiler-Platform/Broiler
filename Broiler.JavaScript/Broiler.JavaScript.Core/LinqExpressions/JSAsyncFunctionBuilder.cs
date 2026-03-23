using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Core.Core.Function;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSAsyncFunctionBuilder
{
    public static YExpression Create(YExpression fx) => NewLambdaExpression.StaticCallExpression<JSFunction>(() => () => JSAsyncFunction.Create(null), fx);
}
