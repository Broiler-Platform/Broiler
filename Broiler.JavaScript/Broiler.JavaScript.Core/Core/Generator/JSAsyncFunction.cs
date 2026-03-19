#nullable enable
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LinqExpressions.GeneratorsV2;
using System;
using YantraJS.Core;
using YantraJS.Core.Generator;

namespace Broiler.JavaScript.Core.Core.Generator;

public class JSAsyncFunction
{
    public static JSFunction Create(JSGeneratorFunctionV2 gf)
    {
        JSValue ToAsync(in Arguments a)
        {
            var gen = gf.InvokeFunction(in a) as JSGenerator;
            return ToPromise(gen!, JSUndefined.Value);
        }

        return new JSFunction(ToAsync, gf.name, gf.Length);
    }

    private static JSValue ToPromise(JSGenerator gen, JSValue lastResult)
    {
        try
        {
            if(!gen.MoveNext(lastResult, out var r))
                return new JSPromise(r, JSPromise.PromiseState.Resolved);

            var then = r[KeyStrings.then];
            if (then.IsUndefined)
                return new JSPromise(r, JSPromise.PromiseState.Resolved);

            r = r.InvokeMethod(in KeyStrings.then, new JSFunction((in Arguments a) =>
            {
                return ToPromise(gen, a.Get1());
            }), 
            new JSFunction((in Arguments a) =>
            {
                gen.Throw(a.Get1());
                return a.Get1();
            }));
            
            return r;
        } 
        catch (Exception ex)
        {
            return new JSPromise(JSError.From(ex), JSPromise.PromiseState.Rejected);
        }
    }
}
