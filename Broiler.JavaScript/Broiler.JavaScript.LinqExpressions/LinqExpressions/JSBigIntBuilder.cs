using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSBigIntBuilder
{
    public static YExpression New(string value) => NewLambdaExpression.StaticCallExpression<JSValue>(
        () => () => JSValue.CreateBigIntFromString(""), YExpression.Constant(value));
}

public class JSDecimalBuilder
{
    public static YExpression New(string value) => NewLambdaExpression.StaticCallExpression<JSValue>(
        () => () => JSValue.CreateDecimalFromString(""), YExpression.Constant(value));
}
