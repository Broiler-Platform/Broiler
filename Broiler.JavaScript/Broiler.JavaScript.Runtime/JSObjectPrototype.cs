using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.Runtime;


public partial class JSObject
{
    [JSExport(IsConstructor = true)]
    public static JSValue Constructor(in Arguments a) 
    {
        if (a.This != null && !a.This.IsUndefined)
            return a.This;

        var first = a.Get1();
        if (first.IsObject)
            return first;

        if (first.IsNullOrUndefined)
            return new JSObject();

        return CreatePrimitiveObject(first as JSPrimitive);
    }

    [JSPrototypeMethod][JSExport("propertyIsEnumerable")]
    public static JSValue PropertyIsEnumerable(in Arguments a)
    {
        if(!a.This.TryAsObjectThrowIfNullOrUndefined(out var @object))
            return JSValue.BooleanFalse;

        if (a.Length > 0)
        {
            var text = a.Get1().ToString();
            var px = @object.GetInternalProperty(text, false);
            if (!px.IsEmpty && px.IsEnumerable)
                return JSValue.BooleanTrue;
        }

        return JSValue.BooleanFalse;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    [JSPrototypeMethod][JSExport("toString")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "JavaScript Method Signature is Standard")]
    public static JSValue ToString(in Arguments a) => JSValue.CreateString("[object Object]");

    [JSExport("__proto__")]
    internal JSValue ObjectPrototype
    {
        get => (prototypeChain as IJSPrototype)?.Object ?? JSValue.NullValue;
        set
        {
            if (value is JSObject o)
            {
                BasePrototypeObject = o;
            }
        }
    }

    [JSPrototypeMethod][JSExport("hasOwnProperty")]
    internal static JSValue HasOwnProperty(in Arguments a)
    {
        if (!a.This.TryAsObjectThrowIfNullOrUndefined(out var @object))
            return JSValue.BooleanFalse;

        var first = a.Get1();
        var key = first.ToKey(false);
        if (key.IsUInt)
        {
            ref var elements = ref @object.GetElements();
            ref var property = ref elements.Get(key.Index);
            if (!property.IsEmpty)
                return JSValue.BooleanTrue;

            return JSValue.BooleanFalse;
        }

        if (key.IsSymbol)
        {
            ref var symbols = ref @object.GetSymbols();
            if (symbols.HasKey(key.Symbol.Key))
                return JSValue.BooleanTrue;
            return JSValue.BooleanFalse;
        }

        ref var op = ref @object.GetOwnProperties(false);
        if (op.HasKey(key.KeyString.Key))
            return JSValue.BooleanTrue;

        return JSValue.BooleanFalse;
    }

    [JSPrototypeMethod]
    [JSExport("valueOf")]
    public static JSValue ValueOf(in Arguments a) => a.This;

    [JSPrototypeMethod][JSExport("isPrototypeOf")]
    internal static JSValue IsPrototypeOf(in Arguments a)
    {
        if (!a.This.TryAsObjectThrowIfNullOrUndefined(out var @this))
            return JSValue.BooleanFalse;

        var first = a.Get1();
        while (true)
        {
            if (@this == (first.prototypeChain as IJSPrototype)?.Object)
                return JSValue.BooleanTrue;

            if ((first.prototypeChain as IJSPrototype)?.Object == first || (first.prototypeChain as IJSPrototype)?.Object == null)
                break;

            first = (first.prototypeChain as IJSPrototype)?.Object;
        }

        return JSValue.BooleanFalse;
    }
}
