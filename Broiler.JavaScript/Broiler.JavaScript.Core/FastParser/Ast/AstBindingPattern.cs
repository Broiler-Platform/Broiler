namespace YantraJS.Core.FastParser;

public class AstBindingPattern(FastToken start, FastNodeType type, FastToken end) : AstExpression(start, type, end, true)
{
}
