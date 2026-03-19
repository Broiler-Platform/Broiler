using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.Generator;

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
