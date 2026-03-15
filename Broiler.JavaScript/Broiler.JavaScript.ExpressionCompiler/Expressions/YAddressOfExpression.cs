using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YAddressOfExpression(YExpression target) : YExpression(YExpressionType.AddressOf, target.Type.IsByRef ? target.Type : target.Type.MakeByRefType())
{
    public readonly YExpression Target = target;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("ref ");
        Target.Print(writer);
    }
}
