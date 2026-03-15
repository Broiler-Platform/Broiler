using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitILOffset(YILOffsetExpression node)
    {
        il.EmitConstant(il.ILOffset);
        return true;
    }
}
