using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitArrayLength(YArrayLengthExpression arrayLengthExpression)
    {
        Visit(arrayLengthExpression.Target);
        il.Emit(OpCodes.Ldlen);
        return true;
    }
}
