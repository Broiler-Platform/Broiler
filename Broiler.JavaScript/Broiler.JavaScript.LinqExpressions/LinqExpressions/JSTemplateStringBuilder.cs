using System.Collections.Generic;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.LinqExpressions.String;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSTemplateStringBuilder
{
    public static Expression New(IEnumerable<Expression> select, int total)
    {
        var list = new Sequence<YElementInit>();
        var newExp = NewLambdaExpression.NewExpression<JSTemplateString>(() => () => new JSTemplateString(0), Expression.Constant(total));
        var en = select.GetEnumerator();

        var addStringMethod = TypeQuery.TypeQuery.QueryInstanceMethod<JSTemplateString>(() => (x) => x.Add(""));
        var addValueMethod = TypeQuery.TypeQuery.QueryInstanceMethod<JSTemplateString>(() => (x) => x.Add((JSValue)null));

        while (en.MoveNext())
        {
            var current = en.Current;
            if (current.NodeType == YExpressionType.Constant)
            {
                list.Add(Expression.ElementInit(addStringMethod, current));
                continue;
            }

            list.Add(Expression.ElementInit(addValueMethod, current));
        }

        return Expression.ListInit(newExp, list).CallExpression<JSTemplateString>(() => (x) => x.ToJSString());
    }
}
