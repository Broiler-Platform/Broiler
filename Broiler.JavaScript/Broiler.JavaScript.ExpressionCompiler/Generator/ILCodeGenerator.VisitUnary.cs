using System;
using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitUnary(YUnaryExpression yUnaryExpression)
    {
        Visit(yUnaryExpression.Target);
        switch(yUnaryExpression.Operator)
        {
            case YUnaryOperator.Negative:
                il.Emit(OpCodes.Neg);
                return true;
            case YUnaryOperator.Not:
                il.EmitConstant(0);
                il.Emit(OpCodes.Ceq);
                return true;
            case YUnaryOperator.OnesComplement:
                il.Emit(OpCodes.Not);
                return true;
        }
        throw new NotImplementedException();
    }
}
