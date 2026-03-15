using System.Reflection.Emit;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitLoop(YLoopExpression yLoopExpression)
    {
        var @continue = labels[yLoopExpression.Continue];
        var @break = labels[yLoopExpression.Break];
        il.MarkLabel(@continue);
        Visit(yLoopExpression.Body);
        if (yLoopExpression.Body.Type != typeof(void))
        {
            il.Emit(OpCodes.Pop);
        }
        il.Branch(@continue);
        il.MarkLabel(@break);
        return true;
    }
}
