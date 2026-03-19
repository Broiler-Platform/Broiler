using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.Debugger;
using Broiler.JavaScript.Core.LambdaGen;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSDebuggerBuilder
{
    public static Expression RaiseBreak() => NewLambdaExpression.StaticCallExpression(() => () => JSDebugger.RaiseBreak());
}
