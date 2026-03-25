using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;
using System;
using System.Runtime.CompilerServices;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public static class JSExceptionBuilder
{
    public static Expression Throw(Expression value) => NewLambdaExpression.StaticCallExpression(() => () => JSException.Throw(null), value);

    public static Expression ThrowSyntaxError(string value) => NewLambdaExpression.StaticCallExpression(() => () => JSException.ThrowSyntaxError(""), Expression.Constant(value));

    public static Expression Throw(string message, Type type = null, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null,
        [CallerLineNumber] int line = 0) => Expression.Throw(NewLambdaExpression.NewExpression<JSException>(() => () => new JSException("", "", "", 0),
            Expression.Constant(message), Expression.Constant(function), Expression.Constant(filePath), Expression.Constant(line)), type ?? typeof(JSValue));


    public static Expression New(string message, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) =>
        Expression.Throw(NewLambdaExpression.NewExpression<JSException>(() => () => new JSException("", "", "", 0), Expression.Constant(message), Expression.Constant(function),
            Expression.Constant(filePath), Expression.Constant(line)));

    public static Expression Wrap(Expression body) => body;
}
