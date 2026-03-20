using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Core.Core.BigInt;
using Broiler.JavaScript.Core.Core.Decimal;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSBigIntBuilder
{
    public static YExpression New(string value) => NewLambdaExpression.NewExpression<JSBigInt>(() => () => new JSBigInt("a"), YExpression.Constant(value));
}

public class JSDecimalBuilder
{
    public static YExpression New(string value) => NewLambdaExpression.NewExpression<JSDecimal>(() => () => new JSDecimal("a"), YExpression.Constant(value));
}
