using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Patterns;

public class AstObjectPattern(FastToken start, FastToken end, IFastEnumerable<ObjectProperty> properties) : AstBindingPattern(start, FastNodeType.ObjectPattern, end)
{
    public readonly IFastEnumerable<ObjectProperty> Properties = properties;
}
