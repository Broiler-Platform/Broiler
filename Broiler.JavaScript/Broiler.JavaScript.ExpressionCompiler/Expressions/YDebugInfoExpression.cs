using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public class YDebugInfoExpression(Position start, Position end) : YExpression(YExpressionType.DebugInfo, typeof(void))
{
    public readonly Position Start = start;
    public readonly Position End = end;

    public override void Print(IndentedTextWriter writer) => writer.WriteLine($"Sequence Point {Start} {End}");
}
