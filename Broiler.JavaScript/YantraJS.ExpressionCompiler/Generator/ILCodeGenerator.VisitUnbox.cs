using System.Reflection.Emit;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitUnbox(YUnboxExpression node)
    {
        Visit(node.Target);
        il.Emit(OpCodes.Unbox_Any, node.Type);
        return true;
    }
}
