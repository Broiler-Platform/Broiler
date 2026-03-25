using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Promise;


public partial class JSPromise
{
    [JSExport("then")]
    public JSValue Then(in Arguments a)
    {
        var (success, fail) = a.Get2();

        if (success is not JSFunction successFx)
            throw JSEngine.NewTypeError($"Parameter for then is not a function");

        if (!fail.IsUndefined)
        {
            if (fail is not JSFunction failFx)
                throw JSEngine.NewTypeError($"Parameter for then is not a function");

            return Then(successFx.f, failFx.f);
        }

        return Then(successFx.f, null);
    }

    [JSExport("catch")]
    public JSValue Catch(JSFunction fx)
    {
        Then(null, fx.f);
        return this;
    }

    [JSExport("finally")]
    public JSValue Finally(JSFunction fx) => Then(fx.f, fx.f);
}
