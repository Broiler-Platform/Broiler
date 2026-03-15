using System.CodeDom.Compiler;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YMemberInitExpression(YNewExpression exp, IFastEnumerable<YBinding> list) : YExpression(YExpressionType.MemberInit, exp.Type)
{
    public readonly YNewExpression Target = exp;
    public readonly IFastEnumerable<YBinding> Bindings = list;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.WriteLine("{");
        writer.Indent++;
        var en = Bindings.GetFastEnumerator();
        while(en.MoveNext(out var b))
        {
            writer.Write(b.Member.Name);
            writer.Write(" = ");
            // b.Value.Print(writer);
            writer.WriteLine(",");
        }
        writer.Indent--;
        writer.Write("}");
    }
}