using System;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public static class JSAsyncFunctionBuilder
{
    private static MethodInfo _createMethod;

    /// <summary>
    /// Initializes the builder with the concrete JSAsyncFunction type.
    /// Called from BuiltInsAssemblyInitializer.
    /// </summary>
    internal static void Initialize(Type asyncFunctionType, Type parameterType)
    {
        _createMethod = asyncFunctionType.GetMethod("Create", [parameterType]);
    }

    public static YExpression Create(YExpression fx) => YExpression.Call(null, _createMethod, fx);
}
