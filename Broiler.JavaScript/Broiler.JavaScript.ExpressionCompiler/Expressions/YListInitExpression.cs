using Broiler.JavaScript.ExpressionCompiler.Core;
using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;



public class YListInitExpression(
    YNewExpression newExpression,
    IFastEnumerable<YElementInit> parameters) : YExpression(YExpressionType.ListInit, newExpression.Type)
{
    public readonly YNewExpression NewExpression = newExpression;
    public readonly IFastEnumerable<YElementInit> Members = parameters;

    public override void Print(IndentedTextWriter writer)
    {
        NewExpression.Print(writer);
        writer.Write(" {");
        writer.Indent++;
        var en = Members.GetFastEnumerator();
        while(en.MoveNext(out var e))
        {
            writer.Write("{");
            var enp = e.Arguments.GetFastEnumerator();
            while(enp.MoveNext(out var p))
            {
                p.Print(writer);
                writer.Write(",");
            }
            writer.WriteLine("},");
        }
        writer.Indent--;
        writer.WriteLine("}");
    }
}
