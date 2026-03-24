using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitBox(YBoxExpression node)
    {
        Visit(node.Target);
        il.Emit(OpCodes.Box, node.Target.Type);
        return true;
    }
}
