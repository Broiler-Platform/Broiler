using System.Linq;
using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitDelegate(YDelegateExpression delegateExpression)
    {
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldftn, delegateExpression.Method);
        var cl = delegateExpression.Type.GetConstructors();
        var c = cl
            .FirstOrDefault(ct => ct.GetParameters().Length == 2);
        il.EmitNew(c);
        if(delegateExpression.Type != typeof(void))
            return true;
        return true;
    }
}
