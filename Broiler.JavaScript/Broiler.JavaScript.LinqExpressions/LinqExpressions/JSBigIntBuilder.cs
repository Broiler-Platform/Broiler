using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.LinqExpressions;

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
