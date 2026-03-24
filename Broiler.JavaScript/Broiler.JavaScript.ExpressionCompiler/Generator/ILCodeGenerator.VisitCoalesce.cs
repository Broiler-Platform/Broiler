using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

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
