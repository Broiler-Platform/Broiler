using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YantraJS.Core;

public class JSClass: JSFunction
{

    internal readonly JSFunction super;
    public JSClass(
        JSFunctionDelegate fx, 
        JSFunction super ,
        string name = null,
        string code = null)
        : base( fx ?? super.f ?? JSFunction.empty, name,code)
    {
        this.super = super;
        BasePrototypeObject = super;
        prototype.BasePrototypeObject = super.prototype;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AddConstructor(JSFunction fx) => f = fx.f;

    public override JSValue InvokeFunction(in Arguments a)
    {
        if (JSContext.NewTarget == null && JSContext.Current.CurrentNewTarget == null)
            throw JSContext.Current.NewTypeError($"{this} is not a function");
        return f(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override JSValue CreateInstance(in Arguments a)
    {
        var @object = new JSObject()
        {
            BasePrototypeObject = prototype
        };
        var ao = a.OverrideThis(@object);
        JSContext.Current.CurrentNewTarget = this;
        var @this = f(ao);
        if (!@this.IsUndefined)
        {
            @this.BasePrototypeObject = prototype;
            return @this;
        }
        return @object;
    }

}
