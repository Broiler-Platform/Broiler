using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{

    protected override CodeInfo VisitDebugInfo(YDebugInfoExpression node)
    {
        SequencePoints.Add(new (il.ILOffset, node.Start, node.End));
        return true;
    }

}
