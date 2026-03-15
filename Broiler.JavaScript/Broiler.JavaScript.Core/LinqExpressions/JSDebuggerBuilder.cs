using YantraJS.Debugger;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LambdaGen;

namespace YantraJS.ExpHelper;

public class JSDebuggerBuilder
{
    //private static Type type = typeof(JSDebugger);

    //private static MethodInfo _RaiseBreak
    //    = type.InternalMethod(nameof(JSDebugger.RaiseBreak));

    public static Expression RaiseBreak() => NewLambdaExpression.StaticCallExpression(() => () => JSDebugger.RaiseBreak());// return Expression.Call(null, _RaiseBreak);
}
