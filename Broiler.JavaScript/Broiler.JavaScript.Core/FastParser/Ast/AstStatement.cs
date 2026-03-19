#nullable enable
using Broiler.JavaScript.Core.FastParser;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstStatement(FastToken start, FastNodeType type, FastToken end) : AstNode(start, type, end, isStatement: true)
{
}
