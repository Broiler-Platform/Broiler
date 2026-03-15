#nullable enable
using System.Collections.Generic;
using YantraJS.Core;
using YantraJS.ExpHelper;
using Expression = YantraJS.Expressions.YExpression;
using ParameterExpression = YantraJS.Expressions.YParameterExpression;
using YantraJS.Expressions;

namespace YantraJS;

internal static class ExpressionHelper
{
    public static void AddExpanded(
        this IList<Expression> list, 
        IList<ParameterExpression> peList,
        Expression exp)
    {

        if(exp.NodeType == YExpressionType.Block)
        {
            var block = (exp as YBlockExpression)!;
            foreach (var p in block.Variables)
                peList.Add(p);
            foreach (var s in block.Expressions)
                list.Add(s);
            return;
        }

        list.Add(exp);
    }


    public static Expression? ToJSValue(this Expression exp)
    {
        if (exp == null)
            return exp;
        if (typeof(JSVariable) == exp.Type)
            return JSVariable.ValueExpression(exp);
        if (typeof(JSValue) == exp.Type)
            return exp;
        if (!typeof(JSValue).IsAssignableFrom(exp.Type))
            return Expression.Block(exp, JSUndefinedBuilder.Value);
        // return Expression.Convert(exp, typeof(JSValue));
        return Expression.TypeAs(exp,typeof(JSValue));
    }

}
