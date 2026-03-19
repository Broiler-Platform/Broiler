using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Generator;

public partial class ILCodeGenerator
{

    protected override CodeInfo VisitDebugInfo(YDebugInfoExpression node)
    {
        SequencePoints.Add(new (il.ILOffset, node.Start, node.End));
        return true;
    }

}
