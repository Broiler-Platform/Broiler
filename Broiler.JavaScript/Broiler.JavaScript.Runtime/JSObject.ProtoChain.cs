using Broiler.JavaScript.Storage;
using System.Runtime.CompilerServices;
namespace Broiler.JavaScript.Runtime;

public partial class JSObject
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSealed() => (status & ObjectStatus.Sealed) > 0;

    public bool IsSealedOrFrozen() => (status & ObjectStatus.SealedOrFrozen) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExtensible() => !((status & ObjectStatus.NonExtensible) > 0);


    public bool IsSealedOrFrozenOrNonExtensible() => (status & ObjectStatus.SealedFrozenNonExtensible) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFrozen() => (status & ObjectStatus.Frozen) > 0;

    internal override PropertyKey ToKey(bool create = true)
    {
        if (!create)
        {
            if (KeyStrings.TryGet(ToString(), out var k))
                return k;

            return KeyStrings.undefined;
        }

        return KeyStrings.GetOrCreate(ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JSProperty GetInternalProperty(in KeyString key, bool inherited = true)
    {
        var r = ownProperties.GetValue(key.Key);
        if (!r.IsEmpty)
            return r;

        if (inherited && prototypeChain != null)
            r = prototypeChain.GetInternalProperty(key);

        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JSProperty GetInternalProperty(uint key, bool inherited = true)
    {
        if (elements.TryGetValue(key, out var r))
            return r;

        if (inherited && prototypeChain != null)
            return prototypeChain.GetInternalProperty(key);

        return new JSProperty();
    }

    internal JSProperty GetInternalProperty(IJSSymbol key, bool inherited = true)
    {
        if (symbols.TryGetValue(key.Key, out var r))
            return r;

        if (inherited && prototypeChain != null)
            return prototypeChain.GetInternalProperty(key);

        return new JSProperty();
    }
    internal override JSFunctionDelegate GetMethod(in KeyString key)
    {
        if (!ownProperties.IsEmpty)
        {
            ref var p = ref ownProperties.GetValue(key.Key);
            if (p.IsValue)
            {
                if (p.get is IJSFunction g)
                    return g.Delegate;
            }

            if (p.IsProperty)
                return (p.get as IJSFunction)?.Delegate;
        }

        return prototypeChain?.GetMethod(key);
    }
}
