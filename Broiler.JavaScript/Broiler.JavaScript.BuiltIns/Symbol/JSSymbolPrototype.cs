using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Symbol;

public partial class JSSymbol
{
    [JSExport(IsConstructor = true)]
    public static JSValue Constructor(in Arguments a)
    {
        var name = a.Get1();
        if (name.IsUndefined)
            return new JSSymbol("");

        return new JSSymbol(name.ToString());
    }
}
