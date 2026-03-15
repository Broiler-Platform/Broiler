using System.CodeDom.Compiler;
using System.Reflection;

namespace YantraJS.Expressions;

public class YFieldExpression(YExpression target, FieldInfo field) : YExpression(YExpressionType.Field, field.FieldType)
{
    public readonly YExpression Target = target;
    public readonly FieldInfo FieldInfo = field;

    public override void Print(IndentedTextWriter writer)
    {
        if(Target==null)
        {
            writer.Write($"{FieldInfo.DeclaringType.GetFriendlyName()}.{FieldInfo.Name}");
            return;
        }
        Target.Print(writer);
        writer.Write('.');
        writer.Write(FieldInfo.Name);
    }
}