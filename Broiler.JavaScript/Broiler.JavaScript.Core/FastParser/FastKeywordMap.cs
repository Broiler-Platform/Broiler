using Broiler.JavaScript.Core.Core.Storage;
using System;

namespace Broiler.JavaScript.Core.FastParser;

public class FastKeywordMap
{

    public static FastKeywordMap Instance = new();

    private static ConcurrentStringMap<FastKeywords> list = ConcurrentStringMap<FastKeywords>.Create();

    static FastKeywordMap()
    {
        foreach (var name in Enum.GetNames(typeof(FastKeywords)))
        {
            var value = (FastKeywords)Enum.Parse(typeof(FastKeywords), name);
            list[name] = value;
        }
    }

    protected FastKeywordMap() { }

    public virtual bool IsKeyword(in StringSpan k, out FastKeywords keyword) => list.TryGetValue(k, out keyword);
}
