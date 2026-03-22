using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Generator;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Enumerators;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.Core.Utils;
using Broiler.JavaScript.ExpressionCompiler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
namespace Broiler.JavaScript.Core;

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

    internal JSProperty GetInternalProperty(JSSymbol key, bool inherited = true)
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
                var g = (JSFunction)p.get;
                if (g != null)
                    return g.f;
            }

            if (p.IsProperty)
                return ((JSFunction)p.get).f;
        }

        return prototypeChain?.GetMethod(key);
    }
}
