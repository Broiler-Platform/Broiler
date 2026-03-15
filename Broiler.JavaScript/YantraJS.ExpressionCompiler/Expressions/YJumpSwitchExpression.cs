using System.CodeDom.Compiler;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YJumpSwitchExpression(YExpression target, IFastEnumerable<YLabelTarget> cases) : YExpression(YExpressionType.JumpSwitch, typeof(void))
{
    public readonly YExpression Target = target;
    public readonly IFastEnumerable<YLabelTarget> Cases = cases;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("switch (");
        Target.Print(writer);
        writer.WriteLine(") {");
        writer.Indent++;
        int i = 0;
        var en = Cases.GetFastEnumerator();
        while(en.MoveNext(out var label))
        {
            writer.Write("case ");
            writer.Write(i++);
            writer.Write(": goto ");
            writer.Write(label.Name);
            writer.WriteLine(";");
        }
        writer.Indent--;
        writer.WriteLine("}");
    }
}
