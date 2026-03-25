using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSContextStackBuilder
{
    public readonly static Type itemTypeRef = typeof(CallStackItem).MakeByRefType();

    public static void Push(Sequence<Expression> stmtList, Expression context, Expression stack, Expression fileName, Expression function, int line, int column)
    {
        var newScope = LexicalScopeBuilder.NewScope(context, fileName, function, line, column);
        stmtList.Add(Expression.Assign(stack, newScope));
    }

    public static Expression Pop(Expression stack, Expression context) => LexicalScopeBuilder.Pop(stack, context);

}

public class JSContextBuilder
{
    private static readonly FieldInfo _CurrentField = typeof(JSEngine).GetField(nameof(JSEngine.Current));
    public static Expression Current = Expression.Field(null, _CurrentField);
    public static Expression Object = Current.PropertyExpression<IJSExecutionContext, JSValue>(() => (x) => x.Object);

    private static PropertyInfo _Index = typeof(JSObject).IndexProperty(typeof(KeyString));
    public static Expression Index(Expression key) => Expression.MakeIndex(Expression.Convert(Current, typeof(JSObject)), _Index, [key]);

    public static Expression NewTarget() => Current.PropertyExpression<IJSExecutionContext, CallStackItem>(() => (x) => x.Top).FieldExpression<CallStackItem, JSValue>(() => (x) => x.NewTarget);

    public static Expression Register(ParameterExpression lScope, ParameterExpression variable) => lScope.CallExpression<IJSExecutionContext, JSVariable, JSValue>(() => (x, a) => x.Register(a), variable);
}
