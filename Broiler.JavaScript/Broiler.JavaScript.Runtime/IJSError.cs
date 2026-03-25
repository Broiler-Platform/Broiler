using System;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over JavaScript error objects, allowing Core types
/// to detect and inspect error objects without depending on the concrete
/// <c>JSError</c> class in BuiltIns.
/// </summary>
public interface IJSError
{
    /// <summary>Gets the .NET exception associated with this error.</summary>
    Exception Exception { get; }

    /// <summary>Gets the error message.</summary>
    string Message { get; }

    /// <summary>Gets the stack trace string.</summary>
    string Stack { get; }
}
