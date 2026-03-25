#nullable enable

namespace Broiler.JavaScript.Runtime;

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
        var handle = @object.handle ??= JSValue.MarshalObject(@object);
        return handle;
    }
}
