using System;
using System.Collections.Generic;
using YantraJS.Core.Core.Storage;

namespace YantraJS.Core;

//internal class ModuleCache: ConcurrentSharedStringTrie<JSModule>
//{
//    internal static Key module = "module";
//    internal static Key clr = "clr";
//}

public struct ModuleCache(bool v)
{
    private static ConcurrentNameMap nameCache;
    private ConcurrentUInt32Map<JSModule> modules = ConcurrentUInt32Map<JSModule>.Create();
    

    static ModuleCache()
    {
        nameCache = new ConcurrentNameMap();
        module = nameCache.Get("module");
        clr = nameCache.Get("clr");
    }

    public static (uint Key, StringSpan Name) module;
    public static (uint Key, StringSpan Name) clr;

    public static ModuleCache Create() => new(true);
    public readonly bool TryGetValue(in StringSpan key, out JSModule obj)
    {
        if(nameCache.TryGetValue(key, out var i))
        {
            if (modules.TryGetValue(i.Key, out obj))
                return true;
        }
        obj = null;
        return false;
    }
    public readonly JSModule GetOrCreate(in StringSpan key, Func<JSModule> factory)
    {
        var k = nameCache.Get(key);
        return modules.GetOrCreate(k.Key, factory);
    }

    public readonly void Add(in StringSpan key, JSModule module)
    {
        var k = nameCache.Get(key);
        modules[k.Key] = module;
    }

    public readonly JSModule this[in (uint Key, StringSpan name) key]
    {
        get {
            if (modules.TryGetValue(key.Key, out var m))
                return m;
            return null;
        }
        set {
            modules[key.Key] = value;
        }
    }

    public readonly IEnumerable<JSModule> All => modules.All;
}
