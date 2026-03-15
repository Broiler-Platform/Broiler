namespace YantraJS.Core.FastParser;

public class AstSwitchStatement(FastToken start, FastToken end, AstExpression target, IFastEnumerable<AstCase> astCases) : AstStatement(start, FastNodeType.SwitchStatement, end)
{
    public readonly AstExpression Target = target;
    public readonly IFastEnumerable<AstCase> Cases = astCases;
}