using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Clr.Tests;

internal static class ClrTestsBootstrap
{
    [ModuleInitializer]
    internal static void EnsureAssembliesLoaded()
    {
        _ = typeof(DefaultClrInterop);
        // Force-load the Compiler assembly and run its module initializer
        // so that the FastCompiler-based pipeline is registered.
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
    }
}
