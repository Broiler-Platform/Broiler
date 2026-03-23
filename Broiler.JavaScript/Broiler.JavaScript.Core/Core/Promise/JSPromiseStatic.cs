using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Error;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Primitive;
using System;
using System.Threading.Tasks;

namespace Broiler.JavaScript.Core;


public partial class JSPromise
{
    public static Task Await(JSValue value)
    {
        if (value.IsNullOrUndefined)
            return System.Threading.Tasks.Task.CompletedTask;

        if (value is JSPromise p)
            return p.Task;


        var then = value["then"];
        if (then.IsNullOrUndefined)
            return System.Threading.Tasks.Task.CompletedTask;

        return new JSPromise((resolve, reject) => then.Call(value, ToFunction(resolve), ToFunction(reject))).Task;

        static JSFunction ToFunction(Action<JSValue> action)
        {
            return new JSFunction((in Arguments a) =>
            {
                action(a[0]);
                return JSUndefined.Value;
            });
        }
    }

    [JSExport("try")]
    public static JSValue Try(in Arguments a)
    {
        var callbackfn = a.Get1();
        if (!callbackfn.IsFunction)
            throw JSContext.NewTypeError("Promise.try requires a callable argument");

        try
        {
            // Collect extra arguments beyond the callback
            var extraArgs = new JSValue[a.Length > 1 ? a.Length - 1 : 0];
            for (int i = 1; i < a.Length; i++)
                extraArgs[i - 1] = a.GetAt(i);

            var callArgs = new Arguments(JSUndefined.Value, extraArgs);
            var result = callbackfn.InvokeFunction(callArgs);
            if (result is JSPromise)
                return result;

            return new JSPromise(result, PromiseState.Resolved);
        }
        catch (JSException ex)
        {
            return new JSPromise(ex.Error ?? JSError.From(ex), PromiseState.Rejected);
        }
        catch (Exception ex)
        {
            return new JSPromise(JSError.From(ex), PromiseState.Rejected);
        }
    }

    [JSExport("resolve")]
    public static JSValue Resolve(in Arguments a) => new JSPromise(a.Get1(), PromiseState.Resolved);

    [JSExport("reject")]
    public static JSValue Reject(in Arguments a)
    {
        var reason = a.Get1();
        if (reason.IsNullOrUndefined)
            throw JSContext.NewTypeError($"Failure reason must be provided for rejected promise");

        return new JSPromise(reason, PromiseState.Rejected);
    }


    [JSExport("all")]
    public static JSValue All(in Arguments a)
    {
        var f = a.Get1();
        var en = f.GetElementEnumerator();
        var result = JSValue.CreateArray();
        uint i = 0;

        return new JSPromise((resolve, reject) =>
        {
            var sc = JSContext.Current.synchronizationContext ?? throw JSContext.NewTypeError($"Cannot use promise without Synchronization Context");
            uint total = 0;

            bool empty = true;

            while (en.MoveNext(out var hasValue, out var e, out var index))
            {
                empty = false;

                if (e is not JSPromise p)
                    throw JSContext.NewTypeError($"All parameters must be Promise");

                var item = e;
                var ni = i++;
                total = i;

                p.Then((in Arguments args) =>
                {
                    var r1 = args.Get1();
                    sc.Post((r) =>
                    {
                        result[ni] = r as JSValue;
                        total--;

                        if (total <= 0)
                            resolve(result);
                    }, r1);
                    return JSUndefined.Value;
                }, (in Arguments args) =>
                {
                    var v = args.Get1();
                    sc.Post((o) => reject(o as JSValue), v);
                    return JSUndefined.Value;
                });
            }

            if (empty)
                sc.Post((o) => resolve(JSValue.CreateArray()), null);
        });
    }
}
