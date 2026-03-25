using System.Runtime.CompilerServices;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Extension methods for <see cref="PropertySequence"/> that require runtime
/// types (<see cref="JSFunctionDelegate"/>). These cannot live on
/// <see cref="PropertySequence"/> itself because it resides in the Storage assembly.
/// </summary>
public static class PropertySequenceRuntimeExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Put(ref this PropertySequence sequence, in KeyString key, JSFunctionDelegate getter, JSFunctionDelegate setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty)
        => sequence.Put(key.Key) = JSPropertyFactory.Property(key, getter, setter, attributes);
}
