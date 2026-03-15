#nullable enable
using System;
using System.CodeDom.Compiler;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YNewArrayExpression(Type type, IFastEnumerable<YExpression> elements) : YExpression( YExpressionType.NewArray, type.MakeArrayType())
{
    public readonly IFastEnumerable<YExpression>? Elements = elements;
    public readonly Type ElementType = type;

    public override void Print(IndentedTextWriter writer)
    {
        if (Elements == null || Elements.Count == 0){
            writer.WriteLine($"new {ElementType.GetFriendlyName()} [] {{}}");
            return;
        }

        writer.WriteLine($"new {ElementType.GetFriendlyName()} [] {{");
        writer.Indent++;
        var en = Elements.GetFastEnumerator();
        while(en.MoveNext(out var a))
        {
            a.Print(writer);
            writer.WriteLine(',');
        }
        writer.Indent--;
        writer.WriteLine("}");
    }
}