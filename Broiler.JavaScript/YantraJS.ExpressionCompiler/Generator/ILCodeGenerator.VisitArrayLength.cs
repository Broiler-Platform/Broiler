using System.Reflection.Emit;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitArrayLength(YArrayLengthExpression arrayLengthExpression)
    {
        Visit(arrayLengthExpression.Target);
        il.Emit(OpCodes.Ldlen);
        return true;
    }
}
