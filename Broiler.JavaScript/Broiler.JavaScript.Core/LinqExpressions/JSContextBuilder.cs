using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.ExpressionCompiler.Core;


namespace Broiler.JavaScript.Core.LinqExpressions;

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
    private static Type type = typeof(JSContext);

    public static Expression Current = NewLambdaExpression.StaticFieldExpression<JSContext>(() => () => JSContext.Current);
    public static Expression Object = Current.FieldExpression<JSContext, JSObject>(() => (x) => x.Object);

    private static PropertyInfo _Index = type.IndexProperty(typeof(KeyString));
    public static Expression Index(Expression key) => Expression.MakeIndex(Current, _Index, [key]);

    public static Expression NewTarget() => Current.FieldExpression<JSContext, CallStackItem>(() => (x) => x.Top).FieldExpression<CallStackItem, JSFunction>(() => (x) => x.NewTarget);

    public static Expression Register(ParameterExpression lScope, ParameterExpression variable) => lScope.CallExpression<JSContext, JSVariable, JSValue>(() => (x, a) => x.Register(a), variable);
}
