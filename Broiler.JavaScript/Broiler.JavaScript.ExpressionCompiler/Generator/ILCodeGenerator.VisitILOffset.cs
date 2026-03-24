using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitILOffset(YILOffsetExpression node)
    {
        il.EmitConstant(il.ILOffset);
        return true;
    }
}
