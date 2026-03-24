using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public static partial class LinqConverters
{
    public static YLambdaExpression ToLLExpression(this LambdaExpression lambda)
    {
        var lc = new LinqConverter();
        return lc.VisitLambdaSpecific(lambda);
    }
}
