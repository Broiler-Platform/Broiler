using System.Linq;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public partial class LinqConverter
{
    protected override YExpression VisitBlock(BlockExpression node)
    {
        var list = Register(node.Variables);
        var s = node.Expressions.Select(Visit).ToArray();
        return YExpression.Block(list, s);
    }

}
