namespace YantraJS.Expressions;

public class YSwitchCaseExpression(YExpression body, YExpression[] testValues)
{
    public readonly YExpression Body = body;
    public readonly YExpression[] TestValues = testValues;
}