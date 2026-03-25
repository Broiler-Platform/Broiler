using System;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Function;

public class JSAsyncFunction
{
    public static JSValue Create(JSValue gf)
    {
        JSValue ToAsync(in Arguments a)
        {
            var gen = gf.InvokeFunction(in a) as IJSGenerator;
            return ToPromise(gen!, JSUndefined.Value);
        }

        var fn = gf as JSFunction;
        return JSValue.CreateFunction(ToAsync, fn?.name.Value, null, gf.Length);
    }

    private static JSValue ToPromise(IJSGenerator gen, JSValue lastResult)
    {
        try
        {
            if(!gen.MoveNext(lastResult, out var r))
                return JSEngine.CreateResolvedOrRejectedPromise(r, true);

            var then = r[KeyStrings.then];
            if (then.IsUndefined)
                return JSEngine.CreateResolvedOrRejectedPromise(r, true);

            r = r.InvokeMethod(in KeyStrings.then, JSValue.CreateFunction((in Arguments a) =>
            {
                return ToPromise(gen, a.Get1());
            }), 
            JSValue.CreateFunction((in Arguments a) =>
            {
                gen.Throw(a.Get1());
                return a.Get1();
            }));
            
            return r;
        } 
        catch (Exception ex)
        {
            return JSEngine.CreateResolvedOrRejectedPromise(JSException.JSErrorFrom(ex), false);
        }
    }
}
