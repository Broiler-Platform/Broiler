using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;

namespace Broiler.JavaScript.Core;


public partial class JSPromise
{
    [JSExport("then")]
    public JSValue Then(in Arguments a)
    {
        var (success, fail) = a.Get2();

        if (!success.IsFunction)
            throw JSContext.NewTypeError($"Parameter for then is not a function");

        if (!fail.IsUndefined)
        {
            if (!fail.IsFunction)
                throw JSContext.NewTypeError($"Parameter for then is not a function");

            return Then(success.FunctionDelegate, fail.FunctionDelegate);
        }

        return Then(success.FunctionDelegate, null);
    }

    [JSExport("catch")]
    public JSValue Catch(JSValue fx)
    {
        Then(null, fx.FunctionDelegate);
        return this;
    }

    [JSExport("finally")]
    public JSValue Finally(JSValue fx) => Then(fx.FunctionDelegate, fx.FunctionDelegate);
}
