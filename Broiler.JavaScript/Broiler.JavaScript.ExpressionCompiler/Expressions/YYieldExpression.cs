using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YYieldExpression(YExpression arg, bool @delegate) : YExpression(YExpressionType.Yield, arg.Type)
{
    public readonly YExpression Argument = arg;
    public readonly bool DelegateYield = @delegate;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("yield ");
        if (DelegateYield)
        {
            writer.Write("*");
        }
        Argument.Print(writer);
    }
}