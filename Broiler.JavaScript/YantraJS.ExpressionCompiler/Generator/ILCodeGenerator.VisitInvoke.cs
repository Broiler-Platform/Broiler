using System;
using System.Reflection;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitInvoke(YInvokeExpression invokeExpression)
    {

        Type type = invokeExpression.Target.Type;
        MethodInfo method = type.GetMethod("Invoke");
        Visit(invokeExpression.Target);
        var a = EmitParameters(method, invokeExpression.Arguments, method.ReturnType);


        il.EmitCall(method);
        a();
        if(method.ReturnType != typeof(void))
            return true;
        return true;
    }
}
