using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YArrayIndexExpression(YExpression target, YExpression index) : YExpression(YExpressionType.ArrayIndex, target.Type.GetElementType())
{
    public readonly YExpression Target = target;
    public new readonly YExpression Index = index;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write("[");
        Index.Print(writer);
        writer.Write("]");
    }
}