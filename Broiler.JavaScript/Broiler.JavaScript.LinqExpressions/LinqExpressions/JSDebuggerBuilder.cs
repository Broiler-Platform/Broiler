using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Debugger;
using Broiler.JavaScript.LinqExpressions.LambdaGen;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSDebuggerBuilder
{
    public static Expression RaiseBreak() => NewLambdaExpression.StaticCallExpression(() => () => JSDebugger.RaiseBreak());
}
