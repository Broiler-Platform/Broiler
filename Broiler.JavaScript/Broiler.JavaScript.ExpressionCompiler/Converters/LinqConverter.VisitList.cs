using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public partial class LinqConverter
{
    private YExpression[] VisitList(IList<Expression> list)
    {
        var r = new YExpression[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var v = Visit(list[i]) ?? throw new ArgumentNullException();
            r[i] = v;
        }
        return r;
    }
}
