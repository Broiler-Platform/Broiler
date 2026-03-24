#nullable enable
using Broiler.JavaScript.Core.LinqExpressions.GeneratorsV2;
using System;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Core.Generator;

public class JSAsyncFunction
{
    public static JSFunction Create(JSGeneratorFunctionV2 gf)
    {
        JSValue ToAsync(in Arguments a)
        {
            var gen = gf.InvokeFunction(in a) as IJSGenerator;
            return ToPromise(gen!, JSUndefined.Value);
        }

        return new JSFunction(ToAsync, gf.name, gf.Length);
    }

    private static JSValue ToPromise(IJSGenerator gen, JSValue lastResult)
    {
        try
        {
            if(!gen.MoveNext(lastResult, out var r))
                return JSContext.CreateResolvedOrRejectedPromise(r, true);

            var then = r[KeyStrings.then];
            if (then.IsUndefined)
                return JSContext.CreateResolvedOrRejectedPromise(r, true);

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
            return JSContext.CreateResolvedOrRejectedPromise(JSException.JSErrorFrom(ex), false);
        }
    }
}
