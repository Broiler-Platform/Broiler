using Broiler.JavaScript.BuiltIns.Promise;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Broiler.JavaScript.BuiltIns.Error;
using Broiler.JavaScript.BuiltIns.Symbol;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Disposable;

public class JSDisposableStack : IJSDisposableStack, IDisposable, IAsyncDisposable
{
    public bool Disposed { get; private set; }
    public bool isAsync { get; private set; }
    public JSValue Error { get; private set; }
    private Stack<(JSValue value, bool async)> stack = new();

    public JSDisposableStack() { }

    public void AddDisposableResource(JSValue value, bool async = false)
    {
        if (value.IsNullOrUndefined)
            return;

        isAsync |= async;
        stack.Push((value, async));
    }

    public JSValue Dispose()
    {
        if (!isAsync)
        {
            ((IDisposable)this).Dispose();
            return JSUndefined.Value;
        }

        var task = DisposeAsync();
        return task.ToPromise();
    }

    void IDisposable.Dispose()
    {
        while (stack.Count > 0)
        {
            var (v, a) = stack.Pop();

            if (a)
                throw JSEngine.NewTypeError("Async resource must not be disposed synchronously.");

            try
            {
                v.InvokeMethod((IJSSymbol)JSSymbol.dispose);
            }
            catch (Exception ex)
            {
                Error = new JSSuppressedError(JSError.From(ex), Error);
            }
        }

        if (Error != null)
            JSException.Throw(Error);
    }

    private async Task DisposeAsync()
    {
        while (stack.Count > 0)
        {
            var (v, a) = stack.Pop();
            try
            {
                if (a)
                {
                    var r = v.InvokeMethod((IJSSymbol)JSSymbol.asyncDispose);
                    await JSPromise.Await(r);
                }
                else
                {
                    v.InvokeMethod((IJSSymbol)JSSymbol.dispose);
                }
            }
            catch (Exception ex)
            {
                Error = new JSSuppressedError(JSError.From(ex), Error);
            }
        }

        if (Error != null)
            JSException.Throw(Error);
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();
}
