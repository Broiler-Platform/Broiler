#nullable enable
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YLabelExpression(YLabelTarget target, YExpression? defaultValue) : YExpression(YExpressionType.Label, target.LabelType)
{
    public readonly YLabelTarget Target = target;
    public readonly YExpression? Default = defaultValue;

    public override void Print(IndentedTextWriter writer)
    {
        if(Default != null)
        {
            writer.Write($"{Target.Name}: (");
            Default.Print(writer);
            writer.Write(")");
            return;
        }
        writer.WriteLine($"{Target.Name}:");
    }
}

public class YGoToExpression(YLabelTarget target, YExpression? defaultValue) : YExpression(YExpressionType.GoTo, target.LabelType)
{
    public readonly YLabelTarget Target = target;

    public readonly YExpression? Default = defaultValue;

    public override void Print(IndentedTextWriter writer)
    {
        if(Default!=null){
            writer.Write($"Goto {Target.Name} with (");
            Default.Print(writer);
            writer.Write(")");
            return;
        }

        writer.Write($"Goto {Target.Name}");
    }
}
public class YReturnExpression(YLabelTarget target, YExpression? defaultValue) : YExpression(YExpressionType.Return, defaultValue?.Type ?? typeof(void))
{
    public readonly YLabelTarget Target = target;
    public readonly YExpression? Default = defaultValue;

    public override void Print(IndentedTextWriter writer)
    {
        if (Default != null)
        {
            writer.Write("RETURN (");
            Default.Print(writer);
            writer.Write($") at {Target.Name}");
            return;
        }

        writer.Write($"RETURN {Target.Name}");
    }

    public YExpression Update(YLabelTarget target, YExpression x) => new YReturnExpression(target, x);
}