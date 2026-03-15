using System;
using System.Linq.Expressions;
using YantraJS.Expressions;

namespace YantraJS.Converters;


public partial class LinqConverter
{
    protected override YExpression VisitCoalesce(BinaryExpression node)
    {
        if (node.Method != null)
            throw new NotSupportedException();
        return YExpression.Coalesce(Visit(node.Left), Visit(node.Right));
    }

}
