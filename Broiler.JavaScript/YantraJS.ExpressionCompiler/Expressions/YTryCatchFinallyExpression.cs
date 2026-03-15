#nullable enable
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YCatchBody(YParameterExpression? parameter, YExpression body)
{
    public readonly YParameterExpression? Parameter = parameter;
    public readonly YExpression Body = body;
}

public class YTryCatchFinallyExpression(
    YExpression @try,
    YCatchBody? @catch,
    YExpression? @finally) : YExpression(YExpressionType.TryCatchFinally, @try.Type)
{
    public readonly YExpression Try = @try;
    public new readonly YCatchBody? Catch = @catch;
    public readonly YExpression? Finally = @finally;

    public override void Print(IndentedTextWriter writer)
    {
        writer.WriteLine("try {");
        writer.Indent++;
        Try.Print(writer);
        writer.Indent--;
        if (Catch != null)
        {
            if (Catch.Parameter != null) {
                writer.WriteLine($"}} catch({Catch.Parameter.Name}) {{");
            }
            else
            {
                writer.WriteLine("} catch {");
            }
            writer.Indent++;
            Catch.Body.Print(writer);
            writer.Indent--;
        }
        if(Finally != null)
        {
            writer.WriteLine("} finally {");
            writer.Indent++;
            Finally.Print(writer);
            writer.Indent--;
        }
        writer.WriteLine("}");
    }
}