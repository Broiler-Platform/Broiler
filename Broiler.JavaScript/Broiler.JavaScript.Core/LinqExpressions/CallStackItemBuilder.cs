using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class CallStackItemBuilder
{
    public static YExpression New(YExpression context, YExpression scriptInfo, int nameOffset, int nameLength, int line, int column) =>
        NewLambdaExpression.NewExpression<CallStackItem>(() => () => new CallStackItem(null, null, 0, 0, 0, 0), context, scriptInfo,
            YExpression.Constant(nameOffset), YExpression.Constant(nameLength), YExpression.Constant(line), YExpression.Constant(column));

    public static YExpression Step(YExpression target, int line, int column) => target.CallExpression<CallStackItem, int, int>(() => (x, a, b) => x.Step(a, b),
            YExpression.Constant(line), YExpression.Constant(column));
}
