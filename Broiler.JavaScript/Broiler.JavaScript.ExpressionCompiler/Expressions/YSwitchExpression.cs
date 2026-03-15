#nullable enable
using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;

namespace YantraJS.Expressions;

public class YSwitchExpression(YExpression target,
    MethodInfo? method,
    YExpression? defaultBody, YSwitchCaseExpression[] cases) : YExpression(YExpressionType.Switch, cases.Last().Body.Type )
{
    public readonly YExpression Target = target;
    public readonly MethodInfo? CompareMethod = method;
    public readonly YExpression? Default = defaultBody;
    public readonly YSwitchCaseExpression[] Cases = cases;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("switch(");
        Target.Print(writer);
        writer.WriteLine(") {");
        writer.Indent++;

        foreach(var @case in Cases)
        {
            foreach(var tv in @case.TestValues)
            {
                writer.Write("case ");
                tv.Print(writer);
                writer.WriteLine(":");
            }
            writer.Indent++;

            @case.Body.Print(writer);
            writer.WriteLine();
            writer.WriteLine("break;");
            writer.Indent--;
        }

        if(Default != null)
        {
            writer.WriteLine("default:");
            writer.Indent++;
            Default.Print(writer);
            writer.WriteLine("break;");
            writer.Indent--;
        }

        writer.Indent--;
        writer.WriteLine("}");
    }
}