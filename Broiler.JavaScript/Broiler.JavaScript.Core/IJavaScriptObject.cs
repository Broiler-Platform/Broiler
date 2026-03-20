#nullable enable

using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;

namespace Broiler.JavaScript.Core;

/// <summary>
/// Moving JavaScript objects in and out of JavaScript Runtime and CLR requires
/// tracking objects through Weak Conditional Table.
/// So IJavaScriptObject interface makes it easy to store constructed ClrProxy handle
/// inside IJavaScriptObject.
/// 
/// If a class implements IJavaScriptObject, only public members with [JSName] attribute
/// are available in JavaScript runtime. And for methods should have the signature
/// JSValue Method(in Arguments a), this helps in reducing the marshaling cost and 
/// implementor can easily improve performance.
/// </summary>
public interface IJavaScriptObject
{
    /// <summary>
    /// This handle makes is easy to reference
    /// object back and forth from CLR and JavaScript runtime.
    /// 
    /// Do not manually modify this.
    /// </summary>
    JSValue? JSHandle { get; set; }
}

public abstract class JavaScriptObject(in Arguments a) : IJavaScriptObject
{
    private JSValue? handle;
    JSValue? IJavaScriptObject.JSHandle
    {
        get => handle;
        set => handle = value;
    }

    public static implicit operator JSValue(JavaScriptObject @object)
    {
        var handle = @object.handle ??= JSContext.ClrInterop.Marshal(@object);
        return handle;
    }
}
