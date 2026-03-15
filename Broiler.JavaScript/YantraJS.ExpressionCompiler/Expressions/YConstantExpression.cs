using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YConstantExpression(object value, Type type) : YExpression(YExpressionType.Constant, type)
{
    public readonly object Value = value;

    public override void Print(IndentedTextWriter writer)
    {
        if (Value == null)
        {
            writer.Write("null");
            return;
        }
        if(Type == typeof(string))
        {
            writer.Write($"\"{Escape(Value.ToString())}\"");
            return;
        }
        writer.Write(Value);
    }

    private string Escape(string text) => text
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\"", "\\\"");
}