using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitILOffset(YILOffsetExpression node)
    {
        il.EmitConstant(il.ILOffset);
        return true;
    }
}
