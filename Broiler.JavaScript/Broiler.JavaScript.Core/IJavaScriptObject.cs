#nullable enable

using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core;

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
        var handle = @object.handle ??= JSEngine.ClrInterop.Marshal(@object);
        return handle;
    }
}
