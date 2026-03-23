#nullable enable
using Broiler;
using Broiler.JavaScript.Core.Core;

#nullable enable
using Broiler.JavaScript.Core.Core.Clr;

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
