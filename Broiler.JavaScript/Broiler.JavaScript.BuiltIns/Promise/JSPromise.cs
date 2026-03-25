using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Runtime;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.JavaScript.BuiltIns.Promise;



[JSFunctionGenerator("Promise")]
public partial class JSPromise : JSObject, IJSPromise
{
    internal enum PromiseState
    {
        Pending,
        Resolved,
        Rejected
    }

    private enum ReactionType
    {
        Resolve,
        Reject
    }

    private class Reaction
    {
        public JSPromise Promise;
        public ReactionType Type;
        public JSFunctionDelegate Handler;
    }

    internal PromiseState state = PromiseState.Pending;

    private Sequence<Reaction> thenList;
    private Sequence<Reaction> rejectList;
    JSFunction resolveFunction;
    JSFunction rejectFunction;
    internal JSValue result = JSUndefined.Value;

    static long nextPromiseID = 1;

    long promiseID;
    ConcurrentDictionary<long, JSValue> pending;

    /// <summary>
    /// .Net removes promises aggressively via
    /// garbage collection... so all promises
    /// till resolved/failed are stored in 
    /// global list
    /// </summary>
    private void RegisterPromise()
    {
        promiseID = Interlocked.Increment(ref nextPromiseID);

        pending = (JSEngine.Current as JSContext)?.PendingPromises;
        pending.TryAdd(promiseID, this);
    }

    internal JSPromise(JSValue value, PromiseState state) : this()
    {
        InitPromise();
        this.state = state;
        result = value;
    }

    /// <summary>
    /// Promise must stay alive till resolved...
    /// </summary>
    /// <param name="value"></param>
    public JSPromise(Task<JSValue> value) : this()
    {
        sc = (JSEngine.Current as JSContext)?.synchronizationContext;
        RegisterPromise();
        value.ContinueWith((t) =>
        {
            if (t.IsCompleted)
            {
                if (t.IsFaulted)
                {
                    Reject(JSException.ErrorFrom(t.Exception));
                    return;
                }

                Resolve(t.Result);
            }
            else
            {
                Reject(JSException.ErrorFrom(t.Exception));
            }
        });
    }

    public JSPromise(in Arguments a) : base(JSEngine.NewTargetPrototype)
    {
        InitPromise();
        JSValue @delegate = a[0];
        try
        {
            @delegate.InvokeFunction(new Arguments(this, resolveFunction, rejectFunction));
        }
        catch (Exception ex)
        {
            rejectFunction.InvokeFunction(new Arguments(JSUndefined.Value, JSException.JSErrorFrom(ex)));
        }
    }

    public JSPromise(JSPromiseDelegate @delegate) : this()
    {
        InitPromise();
        try
        {
            @delegate((v) => resolveFunction.Call(JSUndefined.Value, v), (v) => rejectFunction.Call(JSUndefined.Value, v));
        }
        catch (Exception ex)
        {
            rejectFunction.InvokeFunction(new Arguments(JSUndefined.Value, JSException.JSErrorFrom(ex)));
        }
    }

    private void InitPromise()
    {
        // to improve speed of promise, we will add then/catch here...
        sc = (JSEngine.Current as JSContext)?.synchronizationContext;

        RegisterPromise();

        resolveFunction = new JSFunction((in Arguments a) =>
        {
            Resolve(a.Get1());
            return JSUndefined.Value;
        });

        rejectFunction = new JSFunction((in Arguments a) =>
        {
            Reject(a.Get1());
            return JSUndefined.Value;
        });

    }

    /// <summary>
    /// This prevents garbage collection
    /// </summary>
    public JSPromise Parent { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Resolve(JSValue value)
    {
        if (state != PromiseState.Pending)
            return;

        if (value == this)
        {
            Reject(JSEngine.NewTypeError("A promise cannot be resolved with itself").Error);
            return;
        }

        pending.TryRemove(promiseID, out var __);

        // get then...
        if (value.IsObject)
        {
            var then = value[KeyStrings.then];
            if (then.IsFunction)
            {
                // do what....
                Post(() =>
                {
                    try
                    {
                        then.Call(value, resolveFunction, rejectFunction);
                    }
                    catch (Exception ex)
                    {
                        Reject(JSException.JSErrorFrom(ex));
                    }
                });
                return;
            }
        }

        state = PromiseState.Resolved;
        result = value;

        var thenList = this.thenList;
        if (thenList != null)
        {
            this.thenList = null;
            foreach (var t in thenList)
                Post(t);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Reject(JSValue value)
    {
        if (state != PromiseState.Pending)
            return;

        state = PromiseState.Rejected;
        result = value;

        pending.TryRemove(promiseID, out var __);

        var rejectList = this.rejectList;
        if (rejectList != null)
        {
            this.rejectList = null;
            foreach (var t in rejectList)
                Post(t);
        }
    }

    private TaskCompletionSource<JSValue> taskCompletion = null;
    private SynchronizationContext sc;

    public Task<JSValue> Task
    {
        get
        {
            if (state == PromiseState.Resolved)
                return System.Threading.Tasks.Task.FromResult(result);

            if (state == PromiseState.Rejected)
                throw JSException.FromValue(result);

            if (taskCompletion == null)
            {
                taskCompletion = new TaskCompletionSource<JSValue>();
                Then((in Arguments a) =>
                {
                    taskCompletion.TrySetResult(a.Get1());
                    return JSUndefined.Value;
                }, (in Arguments a) =>
                {
                    taskCompletion.TrySetException(JSException.FromValue(result));
                    return JSUndefined.Value;
                });
            }

            return taskCompletion.Task;
        }
    }

    public override bool ConvertTo(Type type, out object value)
    {
        if (type == typeof(Task<JSValue>) || type == typeof(Task))
        {
            value = Task;
            return true;
        }

        if (type.IsConstructedGenericType)
        {
            if (type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                value = JSPromiseExtensions.ToTaskInternal(this, type);
                return true;
            }
        }

        return base.ConvertTo(type, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JSValue Then(JSFunctionDelegate resolve, JSFunctionDelegate fail, JSPromise @return = null)
    {
        // @return ??= new JSPromise();
        if (@return == null)
        {
            @return = new JSPromise();
            @return.InitPromise();
        }

        var resolved = new Reaction { Promise = @return, Type = ReactionType.Resolve, Handler = resolve };
        var rejected = new Reaction { Promise = @return, Type = ReactionType.Reject, Handler = fail };

        if (state == PromiseState.Pending)
        {
            rejectList ??= [];
            thenList ??= [];
            rejectList.Add(rejected);
            thenList.Add(resolved);
        }
        else if (state == PromiseState.Resolved)
        {
            Post(resolved);
        }
        else
        {
            Post(rejected);
        }

        return @return;
    }

    private void Post(Reaction reaction) => Post(() =>
    {
        if (reaction.Handler != null)
        {
            try
            {
                var handlerResult = reaction.Handler(new Arguments(JSUndefined.Value, result));
                reaction.Promise?.Resolve(handlerResult);
            }
            catch (Exception ex)
            {
                reaction.Promise.Reject(JSException.JSErrorFrom(ex));
            }
        }
        else if (reaction.Type == ReactionType.Resolve)
        {
            reaction.Promise?.Resolve(result ?? JSUndefined.Value);
        }
        else
        {
            reaction.Promise?.Reject(result ?? JSUndefined.Value);
        }
    });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Post(Action action)
    {
        if (sc != null)
            sc.Post(action, (x) => x());
        else
            action();
    }
}
