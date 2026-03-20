using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Ensures the <c>Broiler.JavaScript.Clr</c> and
/// <c>Broiler.JavaScript.Compiler</c> assemblies are loaded before
/// any test code runs.  Their module initializers set up
/// <see cref="Core.JSContext.ClrInterop"/>,
/// <see cref="LinqExpressions.ClrProxyBuilder"/> registrations, and
/// the <see cref="DefaultJSCompiler"/> compilation pipeline.
/// </summary>
internal static class ClrTestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureAssembliesLoaded()
    {
        // Access a static member from the Clr assembly to force loading,
        // which triggers its module initializer.
        _ = Broiler.JavaScript.Clr.DefaultClrInterop.Instance;
        // Force-load the Compiler assembly and run its module initializer
        // so that the FastCompiler-based pipeline is registered.
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
    }
}
