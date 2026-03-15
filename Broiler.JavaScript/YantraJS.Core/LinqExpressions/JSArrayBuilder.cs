using System;
using System.Linq;
using System.Reflection;
using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Expressions;

namespace YantraJS.ExpHelper;

public class JSArrayBuilder
{
    private static Type type = typeof(JSArray);

    public static ConstructorInfo _New =
        type.GetConstructor([]);

    private static ConstructorInfo _NewFromElementEnumerator =
        type.GetConstructor([typeof(IElementEnumerator)]);

    public static MethodInfo _Add =
        type.GetMethod(nameof(Core.JSArray.Add), [typeof(JSValue)]);

    public static MethodInfo _AddRange =
        type.GetMethod(nameof(Core.JSArray.AddRange), [typeof(JSValue)]);


    public static Expression New()
    {
        Expression start = Expression.New(_New);
        return start;
    }

    public static Expression Add(Expression target, Expression p) => Expression.Call(target, _Add, p);

    public static Expression AddRange(Expression target, Expression p) => Expression.Call(target, _AddRange, p);

    public static Expression New(IFastEnumerable<YElementInit> inits) => Expression.ListInit(Expression.New(_New), inits);

    public static Expression New(IFastEnumerable<Expression> list)
    {
        var ei = new Sequence<YElementInit>(list.Count());
        var en = list.GetFastEnumerator();
        while(en.MoveNext(out var e))
        {
            ei.Add(Expression.ElementInit(_Add, [e]));
        }
        return Expression.ListInit(Expression.New(_New), ei);
        //Expression start = Expression.New(_New);
        //foreach (var p in list)
        //{
        //    start = Expression.Call(start, _Add, p);
        //}
        //return start;
    }

    public static Expression NewFromElementEnumerator(Expression en) => Expression.New(_NewFromElementEnumerator, en);


}
