using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitUnbox(YUnboxExpression node)
    {
        Visit(node.Target);
        il.Emit(OpCodes.Unbox_Any, node.Type);
        return true;
    }
}
