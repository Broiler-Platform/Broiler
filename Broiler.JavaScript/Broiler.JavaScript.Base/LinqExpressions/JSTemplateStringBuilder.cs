using System.Collections.Generic;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Expressions;
using YantraJS.Core;
using Broiler.JavaScript.Core.Core.String;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSTemplateStringBuilder
{
    public static Expression New(IEnumerable<Expression> select, int total)
    {
        var list = new Sequence<YElementInit>();
        var newExp = NewLambdaExpression.NewExpression<JSTemplateString>(() => () => new JSTemplateString(0), Expression.Constant(total));
        var en = select.GetEnumerator();

        var addStringMethod = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod<JSTemplateString>(() => (x) => x.Add(""));
        var addValueMethod = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceMethod<JSTemplateString>(() => (x) => x.Add((JSValue)null));

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
