using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Clr.Tests;

internal static class ClrTestsBootstrap
{
    [ModuleInitializer]
    internal static void EnsureAssembliesLoaded()
    {
        // Force-load the Clr assembly and run its module initializer
        // so that ClrInterop, ClrProxyBuilder, and ClrModuleProvider are registered.
        RuntimeHelpers.RunModuleConstructor(typeof(DefaultClrInterop).Module.ModuleHandle);
        // Force-load the Compiler assembly and run its module initializer
        // so that the FastCompiler-based pipeline is registered.
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
    }
}
