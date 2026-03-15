namespace YantraJS.Core.FastParser;

public class AstCallExpression(
    AstExpression previous,
    IFastEnumerable<AstExpression> plist,
    bool coalesce = false) : AstExpression(previous.Start, FastNodeType.CallExpression, plist.Count > 0 ? plist.Last().End : previous.End)
{
    public readonly AstExpression Callee = previous;
    public readonly IFastEnumerable<AstExpression> Arguments = plist;
    public readonly bool Coalesce = coalesce;

    public override string ToString() => $"{Callee}({Arguments.Join()})";
}