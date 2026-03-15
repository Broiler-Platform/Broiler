using System.Reflection.Emit;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitCoalesce(YCoalesceExpression yCoalesceExpression)
    {
        var notNull = il.DefineLabel("coalesce", il.Top);
        Visit(yCoalesceExpression.Left);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brtrue, notNull);
        il.Emit(OpCodes.Pop);

        // is it assign...
        Visit(yCoalesceExpression.Right);
        il.MarkLabel(notNull);
        return true;
    }
}
