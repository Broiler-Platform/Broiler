using System.ComponentModel;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Class;

public class JSClass : JSFunction
{
    internal readonly JSFunction super;
    public JSClass(JSFunctionDelegate fx, JSFunction super, string name = null, string code = null) : base(fx ?? super.f ?? empty, name, code)
    {
        this.super = super;
        BasePrototypeObject = super;
        prototype.BasePrototypeObject = super.prototype;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AddConstructor(JSFunction fx) => f = fx.f;

    public override JSValue InvokeFunction(in Arguments a)
    {
        if (JSEngine.NewTarget == null && (JSEngine.Current as IJSExecutionContext)?.CurrentNewTarget == null)
            throw JSEngine.NewTypeError($"{this} is not a function");

        return f(a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override JSValue CreateInstance(in Arguments a)
    {
        var @object = new JSObject() { BasePrototypeObject = prototype };
        var ao = a.OverrideThis(@object);
        var ec = JSEngine.Current as IJSExecutionContext;
        if (ec != null) ec.CurrentNewTarget = this;
        var @this = f(ao);
        
        if (!@this.IsUndefined)
        {
            @this.BasePrototypeObject = prototype;
            return @this;
        }
        
        return @object;
    }
}
