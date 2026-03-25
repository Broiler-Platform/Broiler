using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;
using System;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSVariableBuilder
{
    public static Expression New(Expression value, string name) => NewLambdaExpression.NewExpression<JSVariable>(() => () =>
    new JSVariable(null as JSValue, ""), value, Expression.Constant(name));// return Expression.New(_New, value, Expression.Constant(name));

    public static Expression NewFromException(Expression value, string name) => NewLambdaExpression.NewExpression<JSVariable>(() => () =>
    new JSVariable(null as Exception, ""), value, Expression.Constant(name));

    public static Expression FromArgument(Expression args, int i, string name) => NewLambdaExpression.NewExpression<JSVariable>(() => () =>
    new JSVariable(Arguments.Empty, 0, ""), args, Expression.Constant(i), Expression.Constant(name));

    public static Expression FromArgumentOptional(Expression args, int i, Expression optional)
    {
        // check if is undefined...
        if (optional == null)
            return ArgumentsBuilder.GetAt(args, i);

        var argAt = ArgumentsBuilder.GetAt(args, i);
        return Expression.Coalesce(JSValueExtensionsBuilder.NullIfUndefined(argAt), optional);
    }

    public static Expression New(string name) => NewLambdaExpression.NewExpression<JSVariable>(() => () =>
    new JSVariable(null as JSValue, ""), JSUndefinedBuilder.Value, Expression.Constant(name));

    public static Expression Property(Expression target) => target.PropertyExpression<JSVariable, JSValue>(() => (x) => x.GlobalValue);
}
