using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Storage;

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
