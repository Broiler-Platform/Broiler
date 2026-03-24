using System;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.LinqExpressions;


public class IElementEnumeratorBuilder
{
    public static Expression Get(Expression target)
    {
        if (typeof(JSValue).IsAssignableFrom(target.Type))
            return target.CallExpression<JSValue, IElementEnumerator>(() => (x) => x.GetElementEnumerator());

        if (ArgumentsBuilder.refType == target.Type || target.Type == typeof(Arguments))
            return ArgumentsBuilder.GetElementEnumerator(target);

        throw new NotImplementedException();
    }

    public static Expression MoveNext(Expression target, Expression item) => target.CallExpression<IElementEnumerator, JSValue, bool>(() => (x, a) => x.MoveNext(out a), item);

    public static Expression AssignMoveNext(Expression assignee, Expression target) => Expression.Assign(assignee,
            target.CallExpression<IElementEnumerator, JSValue, JSValue>(() => (x, a) => x.NextOrDefault(a), JSUndefinedBuilder.Value));
}
