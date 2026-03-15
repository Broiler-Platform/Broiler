#nullable enable
using System.CodeDom.Compiler;
using System.Reflection;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YIndexExpression(YExpression target, PropertyInfo propertyInfo, IFastEnumerable<YExpression> args) : YExpression(YExpressionType.Index, propertyInfo.PropertyType)
{
    public readonly YExpression Target = target;
    public new readonly PropertyInfo Property = propertyInfo;
    public readonly IFastEnumerable<YExpression> Arguments = args;
    public readonly MethodInfo? SetMethod = propertyInfo.SetMethod;
    public readonly MethodInfo? GetMethod = propertyInfo.GetMethod;

    public override void Print(IndentedTextWriter writer)
    {
        Target?.Print(writer);
        writer.Write('[');
        writer.PrintCSV(Arguments);
        writer.Write(']');
    }
}