#nullable enable
using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YConditionalExpression(
    YExpression test,
    YExpression @true,
    YExpression? @false,
    Type? type = null) : YExpression(YExpressionType.Conditional, type ?? @true.Type)
{
    public readonly YExpression test = test;
    public readonly YExpression @true = @true;
    public readonly YExpression? @false = @false;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("if(");
        test.Print(writer);
        writer.Write(')');
        writer.WriteLine(" {");
        writer.Indent++;
        @true.Print(writer);
        writer.Indent--;

        if(@false != null)
        {
            writer.WriteLine("} else {");
            writer.Indent++;
            @false.Print(writer);
            writer.Indent--;
        }

        writer.WriteLine('}');
    }
}