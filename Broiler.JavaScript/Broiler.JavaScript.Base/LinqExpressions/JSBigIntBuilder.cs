using Broiler.JavaScript.Core.LambdaGen;
using YantraJS.Core;
using YantraJS.Core.BigInt;
using YantraJS.Expressions;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSBigIntBuilder
{
    internal static YExpression New(string value) => NewLambdaExpression.NewExpression<JSBigInt>(() => () => new JSBigInt("a"), YExpression.Constant(value));
}

public class JSDecimalBuilder
{
    internal static YExpression New(string value) => NewLambdaExpression.NewExpression<JSDecimal>(() => () => new JSDecimal("a"), YExpression.Constant(value));
}
