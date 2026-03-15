namespace YantraJS.Core.FastParser;

public readonly struct ObjectProperty(
    AstExpression left,
    AstExpression right,
    AstExpression init,
    bool spread = false,
    bool computed = false)
{
    public readonly AstExpression Key = left;
    public readonly AstExpression Value = right;
    public readonly AstExpression Init = init;
    public readonly bool Computed = computed;
    public readonly bool Spread = spread;
}
