using Broiler.JavaScript.BuiltIns.Symbol;
using Broiler.JavaScript.ExpressionCompiler;
using System;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Weak;

[JSClassGenerator("FinalizationRegistry")]
public partial class JSFinalizationRegistry : JSObject
{
    private readonly JSSymbol finalizationSymbol = new("finalization");
    private readonly JSSymbol finalizationToken = new("finalizationToken");

    private readonly JSFunction finalizer;

    public JSFinalizationRegistry(in Arguments a) : base(JSEngine.NewTargetPrototype)
    {
        if (a[0] is not JSFunction fx)
            throw JSEngine.NewTypeError($"Argument is not a function");

        finalizer = fx;
    }

    internal class WeakObject(JSFinalizationRegistry registry, JSValue token) : JSObject
    {
        ~WeakObject()
        {
            registry.FinalizeReference(token);
        }
    }

    private void FinalizeReference(JSValue token)
    {
        token.Delete((IJSSymbol)finalizationToken);
        finalizer.InvokeFunction(new Arguments(this, token));
    }

    [JSExport]
    public JSValue Unregister(in Arguments a)
    {
        if (a[0] is not JSObject)
            throw JSEngine.NewTypeError($"Argument is not an object");

        Unregister(a[0]);
        return JSUndefined.Value;
    }

    [JSExport]
    public JSValue Register(in Arguments a)
    {
        if (a[0] is not JSObject obj)
            throw JSEngine.NewTypeError($"Argument is not an object");

        var token = a[1];
        if (token?.IsNullOrUndefined ?? false)
            throw JSEngine.NewTypeError($"Token is required");

        Register(obj, token);
        return JSUndefined.Value;
    }

    private void Register(JSValue target, JSValue token)
    {
        var weakRef = new WeakObject(this, token);
        target[(IJSSymbol)finalizationSymbol] = weakRef;
        token[(IJSSymbol)finalizationToken] = weakRef;
    }

    private void Unregister(JSValue token)
    {
        var weakRef = token[(IJSSymbol)finalizationSymbol];
        token.Delete((IJSSymbol)finalizationSymbol);
        GC.SuppressFinalize(weakRef);
    }
}

[JSClassGenerator("WeakRef")]
public partial class JSWeakRef : JSObject
{
    internal WeakReference<JSValue> weak;
    public JSWeakRef(JSValue value) : this() => weak = new WeakReference<JSValue>(value);
    public JSWeakRef(in Arguments a) : base(JSEngine.NewTargetPrototype) => weak = new WeakReference<JSValue>(a[0] ?? throw new JSException($"argument is missing"));

    [JSExport]
    public JSValue Deref(in Arguments a)
    {
        if (weak.TryGetTarget(out var v))
            return v;

        return JSUndefined.Value;
    }
}
