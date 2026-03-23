namespace Broiler.JavaScript.Core.Core;

internal static class UniqueID
{
    internal static string ToUniqueID(this JSValue value) => value switch
    {
        JSValue v when v.IsString => $"string:{v.StringValue}",
        JSValue n when n.IsNumber => $"number:{n.DoubleValue}",
        JSObject @object => $"id:{@object.UniqueID}",
        JSSymbol symbol => $"symbol:{symbol.Key}",
        _ => value.ToString(),
    };
}
