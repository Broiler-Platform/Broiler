using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{
    private static readonly JSFunctionDelegate ReturnUndefinedDelegate = ReturnUndefined;
    private static readonly JSFunctionDelegate ReturnNullDelegate = ReturnNull;
    private static readonly JSFunctionDelegate ReturnTrueDelegate = ReturnTrue;
    private static readonly JSFunctionDelegate ReturnZeroDelegate = ReturnZero;

    private static JSFunction UndefinedFunction(string name, int length = 0)
    {
        return new JSFunction(ReturnUndefinedDelegate, name, length);
    }

    private static JSFunction NullFunction(string name, int length = 0)
    {
        return new JSFunction(ReturnNullDelegate, name, length);
    }

    private static JSFunction TrueFunction(string name, int length = 0)
    {
        return new JSFunction(ReturnTrueDelegate, name, length);
    }

    private static JSFunction ZeroFunction(string name, int length = 0)
    {
        return new JSFunction(ReturnZeroDelegate, name, length);
    }

    private static JSValue ReturnUndefined(in Arguments _)
    {
        return JSUndefined.Value;
    }

    private static JSValue ReturnNull(in Arguments _)
    {
        return JSNull.Value;
    }

    private static JSValue ReturnTrue(in Arguments _)
    {
        return JSBoolean.True;
    }

    private static JSValue ReturnZero(in Arguments _)
    {
        return new JSNumber(0);
    }
}
