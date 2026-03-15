#nullable enable
namespace YantraJS.Core.FastParser;

public class AstStatement(FastToken start, FastNodeType type, FastToken end) : AstNode(start, type, end, isStatement: true)
{
}
