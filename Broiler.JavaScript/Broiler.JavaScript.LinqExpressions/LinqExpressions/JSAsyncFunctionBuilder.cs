using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSAsyncFunctionBuilder
{
    private static System.Reflection.MethodInfo _createMethod =
        typeof(JSAsyncFunction).GetMethod(nameof(JSAsyncFunction.Create), [typeof(Broiler.JavaScript.Core.LinqExpressions.GeneratorsV2.JSGeneratorFunctionV2)]);

    public static YExpression Create(YExpression fx) => YExpression.Call(null, _createMethod, fx);
}
