using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Parser;

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
