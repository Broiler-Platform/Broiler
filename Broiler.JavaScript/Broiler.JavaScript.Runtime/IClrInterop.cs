using System;

namespace Broiler.JavaScript.Runtime;

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

    /// <summary>
    /// Attempts to unwrap a <see cref="JSValue"/> that represents a CLR
    /// object proxy, returning the underlying .NET object.  This replaces
    /// direct <c>is ClrProxy</c> type checks so that non-Clr assemblies
    /// can inspect proxy values without referencing the concrete type.
    /// </summary>
    /// <param name="value">The JavaScript value to inspect.</param>
    /// <param name="clrObject">
    /// When the method returns <c>true</c>, contains the wrapped .NET
    /// object; otherwise <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is a CLR proxy wrapping a
    /// .NET object; <c>false</c> otherwise.
    /// </returns>
    bool TryUnwrapClrObject(JSValue value, out object clrObject);
}
