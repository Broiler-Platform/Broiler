using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Ensures the <c>Broiler.JavaScript.Clr</c> assembly is loaded before
/// any test code runs.  The Clr assembly's module initializer sets up
/// <see cref="Core.JSContext.ClrInterop"/> and
/// <see cref="LinqExpressions.ClrProxyBuilder"/> registrations.
/// </summary>
internal static class ClrTestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureClrLoaded()
    {
        // Access a static member from the Clr assembly to force loading,
        // which triggers its module initializer.
        _ = Broiler.JavaScript.Clr.DefaultClrInterop.Instance;
    }
}
