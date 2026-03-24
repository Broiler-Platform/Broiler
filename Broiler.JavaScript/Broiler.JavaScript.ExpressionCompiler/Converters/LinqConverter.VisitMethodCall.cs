using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public partial class LinqConverter
{
    protected YExpression[] Visit(IEnumerable<Expression> list) => [.. list.Select(Visit)];

    protected override YExpression VisitCall(MethodCallExpression node)
    {
        var target = Visit(node.Object);
        var list = node.Arguments.Select(Visit).ToArray();
        return YExpression.Call(target, node.Method, list);
    }
}
