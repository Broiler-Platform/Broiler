using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.String;

public partial class JSString
{
    [JSExport(Length = 1, IsConstructor = true)]
    public static JSValue Constructor(in Arguments a)
    {
        if (a.Length == 0)
            return new JSPrimitiveObject(new JSString(StringSpan.Empty));

        return new JSPrimitiveObject(new JSString(a.Get1().ToString()));
    }
}
