using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.Core;

/// <summary>
/// Extension methods and Core-dependent helpers for <see cref="KeyString"/>
/// and <see cref="KeyStrings"/>.  The bulk of the KeyString/KeyStrings
/// implementation lives in the Storage assembly; this file retains only
/// the methods that depend on Core types (JSString, JSValue).
/// </summary>
public static class KeyStringCoreExtensions
{
    /// <summary>
    /// Converts a <see cref="KeyString"/> to its <see cref="JSValue"/>
    /// representation (a <see cref="JSString"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue ToJSValue(this KeyString ks) => new JSString(KeyStrings.GetNameString(ks.Key), ks);

    /// <summary>
    /// Returns the <see cref="JSString"/> for the given key ID.
    /// Equivalent to the former <c>KeyStrings.GetJSString</c> method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSString GetJSString(uint id)
    {
        var name = KeyStrings.GetName(id);
        return new JSString(name.Value, name);
    }
}
