using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{

    protected override CodeInfo VisitParameter(YParameterExpression yParameterExpression)
    {
        // check if it is marked as a closure...

        if (closureRepository.TryGet(yParameterExpression, out var ve))
        {
            InitializeClosure(yParameterExpression);
            return Visit(ve);
        }

        var v = variables[yParameterExpression];
        il.Comment($"Load {v.Name}");
        var isValueType = yParameterExpression.Type.IsValueType;
        if (isValueType)
        {
            if (v.IsArgument)
            {
                il.EmitLoadArg(v.Index);
                return true;
            }

            il.EmitLoadLocal(v.LocalBuilder.LocalIndex);
            return true;
        }
        if (v.IsArgument)
        {
            // irrespective of RequiresAddress
            // retype always load ref...
            il.EmitLoadArg(v.Index);
            return true;
        }

        il.EmitLoadLocal(v.LocalBuilder.LocalIndex);
        if (v.IsReference)
        {
            throw new NotSupportedException();
        }

        return true;
    }
}
