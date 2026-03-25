
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Events;

public class CustomEvent : Event
{
    public CustomEvent(in Arguments a) : base(a)
    {
        var options = a[1];
        if (options == null || options.IsUndefined || options.IsNull)
            return;
        
        Detail = options[KeyStrings.detail];
    }

    [JSExport]
    public JSValue? Detail { get; }
}
