using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YUnaryExpression(YExpression exp, YUnaryOperator @operator) : YExpression(YExpressionType.Unary, exp.Type)
{
    public readonly YExpression Target = exp;
    public readonly YUnaryOperator Operator = @operator;

    public override void Print(IndentedTextWriter writer)
    {
        switch (Operator)
        {
            case YUnaryOperator.Not:
                writer.Write("~(");
                Target.Print(writer);
                writer.Write(")");
                break;
            case YUnaryOperator.Negative:
                writer.Write("!(");
                Target.Print(writer);
                writer.Write(")");
                break;
        }
    }
}