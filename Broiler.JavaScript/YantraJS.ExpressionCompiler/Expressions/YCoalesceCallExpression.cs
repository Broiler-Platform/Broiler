using System.CodeDom.Compiler;
using System.Reflection;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YCoalesceCallExpression(
    YExpression target,
    MemberInfo test,
    IFastEnumerable<YExpression> testArguments,
    MethodInfo @true,
    IFastEnumerable<YExpression> trueArguments,
    MethodInfo @false,
    IFastEnumerable<YExpression> falseArguments
    ) : YExpression(YExpressionType.CoalesceCall, @true?.ReturnType ?? @false.ReturnType)
{
    public readonly YExpression Target = target;
    public readonly MemberInfo Test = test;
    public readonly IFastEnumerable<YExpression> TestArguments = testArguments;
    public readonly MethodInfo True = @true;
    public readonly IFastEnumerable<YExpression> TrueArguments = trueArguments;
    public readonly MethodInfo False = @false;
    public readonly IFastEnumerable<YExpression> FalseArguments = falseArguments;

    public override void Print(IndentedTextWriter writer)
    {
        
    }
}
