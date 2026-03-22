using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;

public partial class FastScope : LinkedStack<FastScopeItem>
{
    public FastScope()
    {
    }

    public FastScopeItem Push(FastToken token, FastNodeType nodeType)
    {
        var n = new FastScopeItem(nodeType);
        return Push(n);
    }
}
