using System;
using System.Linq;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSArrayBuilder
{
    private static Type type;

    public static ConstructorInfo _New;
    private static ConstructorInfo _NewFromElementEnumerator;
    public static MethodInfo _Add;
    public static MethodInfo _AddRange;

    /// <summary>
    /// Initializes the builder with the concrete JSArray type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type arrayType)
    {
        type = arrayType;
        _New = type.GetConstructor([]);
        _NewFromElementEnumerator = type.GetConstructor([typeof(IElementEnumerator)]);
        _Add = type.GetMethod("Add", [typeof(JSValue)]);
        _AddRange = type.GetMethod("AddRange", [typeof(JSValue)]);
    }

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

        while (en.MoveNext(out var e))
            ei.Add(Expression.ElementInit(_Add, [e]));

        return Expression.ListInit(Expression.New(_New), ei);
    }

    public static Expression NewFromElementEnumerator(Expression en) => Expression.New(_NewFromElementEnumerator, en);
}
