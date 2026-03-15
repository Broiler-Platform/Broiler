using System;

namespace YantraJS.Core.Clr;

/// <summary>
/// Defines the contract for marshalling between .NET (CLR) objects and
/// JavaScript values.  Implementations bridge the type systems so that
/// .NET instances can be used from JavaScript and vice-versa.
/// </summary>
public interface IClrInterop
{
    /// <summary>
    /// Converts an arbitrary .NET object to its JavaScript representation.
    /// Primitive types are mapped to their JS equivalents (number, string,
    /// boolean); complex objects are wrapped in a <see cref="ClrProxy"/>.
    /// </summary>
    /// <param name="value">The .NET value to marshal. May be <c>null</c>.</param>
    /// <returns>A <see cref="JSValue"/> representing <paramref name="value"/>.</returns>
    JSValue Marshal(object value);

    /// <summary>
    /// Returns the JavaScript class wrapper for the specified .NET
    /// <see cref="Type"/>, allowing JavaScript code to construct instances
    /// and access static members.
    /// </summary>
    /// <param name="type">The .NET type to wrap.</param>
    /// <returns>A <see cref="JSValue"/> representing the type as a JS constructor.</returns>
    JSValue GetClrType(Type type);
}
