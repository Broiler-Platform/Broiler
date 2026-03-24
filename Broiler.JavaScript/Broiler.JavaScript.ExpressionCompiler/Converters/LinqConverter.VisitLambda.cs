using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public partial class LinqConverter
{
    protected override YExpression VisitLambda(LambdaExpression node) => VisitLambdaSpecific(node);
    public YLambdaExpression VisitLambdaSpecific(LambdaExpression lambda)
    {
        var plist = Register(lambda.Parameters);
        return YExpression.Lambda(lambda.Type, lambda.Name ?? "unnamed", Visit(lambda.Body), [.. plist]);
    }
}
