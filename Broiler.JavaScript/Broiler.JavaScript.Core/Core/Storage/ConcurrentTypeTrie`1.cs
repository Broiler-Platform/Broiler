using System;
using YantraJS.Core.Core.Storage;

namespace YantraJS.Core.Storage;

internal class ConcurrentTypeTrie<T>(Func<Type, T> factory)
{
    readonly ConcurrentUInt32Map<T> cache = ConcurrentUInt32Map<T>.Create();

    public T this[Type key]
    {
        get
        {
            var k = ConcurrentTypeCache.GetOrCreate(key);
            return cache.GetOrCreate(k, () => factory(key));
        }
    }
}
