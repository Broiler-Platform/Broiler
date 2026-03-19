using System.Reflection.Emit;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitField(YFieldExpression yFieldExpression)
    {

        var field = yFieldExpression.FieldInfo;
        if (field.IsStatic)
        {

            if (field.IsLiteral)
            {
                il.EmitConstant( field.GetRawConstantValue());
                return true;
            }

            il.Emit(OpCodes.Ldsfld, field);
            return true;
        }

        Visit(yFieldExpression.Target);

        il.Emit(OpCodes.Ldfld, field);
        return true;
    }
}
