namespace YantraJS.Core.FastParser;

public class AstEmptyExpression(FastToken start, bool isBinding = false) : AstExpression(start, FastNodeType.EmptyExpression, start, isBinding)
{
    public override string ToString() => "<<Empty>>";
}
