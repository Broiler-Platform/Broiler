using Broiler.JavaScript.Runtime;
using System;
using System.Threading.Tasks;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Promise;


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
        var receiver = a.This;
        if (!receiver.IsObject)
            throw JSEngine.NewTypeError("Promise.try receiver must be an object");

        if (receiver is not JSFunction constructor || constructor.prototype == null)
            throw JSEngine.NewTypeError("Promise.try receiver must be a constructor");

        var callbackfn = a.Get1();
        if (!callbackfn.IsFunction)
            throw JSEngine.NewTypeError("Promise.try requires a callable argument");

        var extraArgs = new JSValue[a.Length > 1 ? a.Length - 1 : 0];
        for (int i = 1; i < a.Length; i++)
            extraArgs[i - 1] = a.GetAt(i);

        var executor = JSValue.CreateFunction((in Arguments executorArgs) =>
        {
            var resolve = executorArgs.Get1();
            var reject = executorArgs.GetAt(1);

            try
            {
                var result = callbackfn.InvokeFunction(new Arguments(JSUndefined.Value, extraArgs));
                resolve.InvokeFunction(new Arguments(JSUndefined.Value, result));
            }
            catch (JSException ex)
            {
                reject.InvokeFunction(new Arguments(JSUndefined.Value, ex.Error ?? JSException.JSErrorFrom(ex)));
            }
            catch (Exception ex)
            {
                reject.InvokeFunction(new Arguments(JSUndefined.Value, JSException.JSErrorFrom(ex)));
            }

            return JSUndefined.Value;
        }, "executor", length: 2, createPrototype: false);

        return constructor.CreateInstance(new Arguments(JSUndefined.Value, executor));
    }

    [JSExport("resolve")]
    public static JSValue Resolve(in Arguments a) => new JSPromise(a.Get1(), PromiseState.Resolved);

    [JSExport("reject")]
    public static JSValue Reject(in Arguments a)
    {
        var reason = a.Get1();
        if (reason.IsNullOrUndefined)
            throw JSEngine.NewTypeError($"Failure reason must be provided for rejected promise");

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
            var sc = (JSEngine.Current as JSContext)?.synchronizationContext ?? throw JSEngine.NewTypeError($"Cannot use promise without Synchronization Context");
            uint total = 0;

            bool empty = true;

            while (en.MoveNext(out var hasValue, out var e, out var index))
            {
                empty = false;

                if (e is not JSPromise p)
                    throw JSEngine.NewTypeError($"All parameters must be Promise");

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
