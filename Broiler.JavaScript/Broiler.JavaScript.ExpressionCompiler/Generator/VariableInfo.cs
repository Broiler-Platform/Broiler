using System.Collections.Generic;
using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;


public class VariableInfo(ILGenerator il)
{
    private Dictionary<YParameterExpression, Variable> variables 
        = new(Core.ReferenceEqualityComparer.Instance);

    public Variable this[YParameterExpression exp]
    {
        get => variables[exp];
    }

    public Variable Create(
        YParameterExpression exp, 
        bool isArgument = false, 
        short index = -1)
    {
        var vb = new Variable(il.DeclareLocal(exp.Type), isArgument, index, exp.Type.IsByRef, exp.Name);
        variables[exp] = vb;
        return vb;
    }

    public Variable Create(
        YParameterExpression exp,
        TempVariables.TempVariableItem tvs)
    {
        var vb = new Variable(tvs.Get(exp.Type), false, -1, exp.Type.IsByRef, exp.Name);
        variables[exp] = vb;
        return vb;
    }


}
