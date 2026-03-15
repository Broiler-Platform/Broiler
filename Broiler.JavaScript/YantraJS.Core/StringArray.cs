using YantraJS.Core;
using YantraJS.Core.Core.Storage;

namespace YantraJS;

public class StringArray
{
    private StringMap<uint> map;
    
    public Sequence<StringSpan> List { get; } = [];
    
    public uint GetOrAdd(in StringSpan code)
    {
        if (map.TryGetValue(code, out var i))
            return i;
        i = (uint)List.Count;
        map.Put(code) = i;
        List.Add(code);
        return i;
    }
}
