using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.LinqExpressions.LambdaGen;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSGeneratorFunctionBuilderV2
{
    static System.Type type;

    /// <summary>
    /// Initializes the builder with the concrete JSGeneratorFunctionV2 type.
    /// Called from BuiltInsAssemblyInitializer.
    /// </summary>
    internal static void Initialize(System.Type generatorFunctionType)
    {
        type = generatorFunctionType;
    }

    public static Expression New(Expression @delegate, Expression name, Expression code) =>
        NewLambdaExpression.NewExpression(type, @delegate, name, code);
}
