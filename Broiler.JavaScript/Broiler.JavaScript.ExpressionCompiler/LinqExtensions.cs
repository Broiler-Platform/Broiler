using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Converters;
using Broiler.JavaScript.ExpressionCompiler.Runtime;

namespace Broiler.JavaScript.ExpressionCompiler;

public static class LinqExtensions
{

    public static T CompileInAssembly<T>(this Expression<T> expression)
    {
        var ll = LinqConverters.ToLLExpression(expression);
        return ll.As<T>().CompileInAssembly();
    }


    public static T FastCompileWithoutNested<T>(this Expression<T> expression)
    {
        var ll = LinqConverters.ToLLExpression(expression);
        return ll.As<T>().Compile();
    }

    public static T FastCompile<T>(this Expression<T> expression)
    {
        var ll = LinqConverters.ToLLExpression(expression);
        return ll.As<T>().CompileWithNestedLambdas();
    }
}
