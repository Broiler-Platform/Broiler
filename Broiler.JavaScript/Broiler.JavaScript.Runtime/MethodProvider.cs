using Broiler.JavaScript.Storage;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Broiler.JavaScript.Runtime;

public abstract class MethodProvider
{
    public static MethodProvider Current = new ListMethodProvider();
    public abstract Expression Compile(LambdaExpression expression);
    public abstract T Get<T>(int index);
}

public class ListMethodProvider : MethodProvider
{
    private ConcurrentUInt32Map<object> list = ConcurrentUInt32Map<object>.Create();

    private static Type type = typeof(MethodProvider);
    private static MethodInfo @get = type.GetMethod("Get");
    private static readonly Expression CurrentProperty = Expression.Field(null, type.GetField("Current"));

    private static int mi = 0;

    public override Expression Compile(LambdaExpression expression)
    {
        var d = expression.Compile();
        int index = Interlocked.Increment(ref mi);

        list[(uint)index] = d;

        var m = get.MakeGenericMethod(expression.Type);
        return Expression.Call(CurrentProperty, m, Expression.Constant(index));
    }

    public override T Get<T>(int index) => (T)list[(uint)index];
}
