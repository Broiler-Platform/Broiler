using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YLoopExpression(YExpression body, YLabelTarget @break, YLabelTarget @continue) : YExpression(YExpressionType.Loop, @break.LabelType)
{
    public readonly YExpression Body = body;
    public readonly new YLabelTarget Break = @break;
    public readonly new YLabelTarget Continue = @continue;

    public override void Print(IndentedTextWriter writer)
    {
        writer.WriteLine("while(true) {");
        writer.Indent++;
        Body.Print(writer);
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine($"{Break.Name}:");
    }
}