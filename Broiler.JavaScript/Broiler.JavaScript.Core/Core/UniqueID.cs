using YantraJS.Core;

namespace Broiler.JavaScript.Core.Core;

internal static class UniqueID
{
    internal static string ToUniqueID(this JSValue value) => value switch
    {
        JSString @string => $"string:{@string}",
        JSNumber n => $"number:{n.value}",
        JSObject @object => $"id:{@object.UniqueID}",
        JSSymbol symbol => $"symbol:{symbol.Key}",
        _ => value.ToString(),
    };
}
