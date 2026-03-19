#nullable enable
namespace Broiler.JavaScript.Ast;

public class AstStatement(FastToken start, FastNodeType type, FastToken end) : AstNode(start, type, end, isStatement: true)
{
}
