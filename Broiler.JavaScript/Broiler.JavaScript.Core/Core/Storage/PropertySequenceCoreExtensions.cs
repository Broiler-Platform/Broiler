using System.Runtime.CompilerServices;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Core.Core.Storage;

/// <summary>
/// Extension methods for <see cref="PropertySequence"/> that require Core runtime
/// types (<see cref="JSFunctionDelegate"/>). These cannot live on
/// <see cref="PropertySequence"/> itself because it resides in the Storage assembly.
/// </summary>
public static class PropertySequenceCoreExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Put(ref this PropertySequence sequence, in KeyString key, JSFunctionDelegate getter, JSFunctionDelegate setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty)
        => sequence.Put(key.Key) = JSPropertyFactory.Property(key, getter, setter, attributes);

    /// <summary>
    /// Initializes the <see cref="PropertySequence.TypeErrorFactory"/> delegate
    /// so that property deletion errors produce the correct JavaScript TypeError
    /// exception. Called during Core assembly initialization.
    /// </summary>
    [ModuleInitializer]
    internal static void InitializeTypeErrorFactory()
    {
        PropertySequence.TypeErrorFactory = msg => JSContext.NewTypeError(msg);
    }
}
