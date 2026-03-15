using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YCoalesceExpression(YExpression left, YExpression right) : YExpression(YExpressionType.Coalesce, left.Type)
{
    public readonly YExpression Left = left;
    public readonly YExpression Right = right;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("(");
        Left.Print(writer);
        writer.Write(" ?? ");
        Right.Print(writer);
        writer.Write(")");
    }
}