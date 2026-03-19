using System;
using System.Threading;

namespace Broiler.JavaScript.Storage;

public static class ConcurrentTypeCache
{
    private static int nextId = 0;
    private static readonly ConcurrentStringMap<uint> cache = ConcurrentStringMap<uint>.Create();

    public static uint GetOrCreate(Type name, string suffix = "") => cache.GetOrCreate(name.GetHashCode() + ":" + name.FullName + suffix, (_) => (uint)Interlocked.Increment(ref nextId));
}
