using Broiler.JavaScript.Storage;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Extension methods and Core-dependent helpers for <see cref="KeyString"/>
/// and <see cref="KeyStrings"/>.  The bulk of the KeyString/KeyStrings
/// implementation lives in the Storage assembly; this file retains only
/// the methods that depend on Runtime types (JSValue).
/// </summary>
public static class KeyStringCoreExtensions
{
    /// <summary>
    /// Converts a <see cref="KeyString"/> to its <see cref="JSValue"/>
    /// representation (a string value).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue ToJSValue(this KeyString ks) => JSValue.CreateStringWithKey(ks.ToString(), ks);

    /// <summary>
    /// Returns the <see cref="JSValue"/> string for the given key ID.
    /// Equivalent to the former <c>KeyStrings.GetJSString</c> method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSValue GetJSString(uint id)
    {
        var name = KeyStrings.GetName(id);
        return JSValue.CreateStringWithKey(name.ToString(), name);
    }
}
