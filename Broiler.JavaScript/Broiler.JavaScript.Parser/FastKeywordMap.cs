using Broiler.JavaScript.Ast.Misc;
using System;
using System.Collections.Concurrent;

namespace Broiler.JavaScript.Parser;

public class FastKeywordMap
{

    public static FastKeywordMap Instance = new();

    private static ConcurrentDictionary<string, FastKeywords> list = new();

    static FastKeywordMap()
    {
        foreach (var name in Enum.GetNames(typeof(FastKeywords)))
        {
            var value = (FastKeywords)Enum.Parse(typeof(FastKeywords), name);
            list[name] = value;
        }
    }

    protected FastKeywordMap() { }

    public virtual bool IsKeyword(in StringSpan k, out FastKeywords keyword) => list.TryGetValue(k.Value, out keyword);
}
