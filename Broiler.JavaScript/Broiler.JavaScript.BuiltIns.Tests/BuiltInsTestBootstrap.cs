using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core.Weak;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.BuiltIns.Tests;

/// <summary>
/// Ensures the <c>Broiler.JavaScript.Clr</c>, <c>Broiler.JavaScript.Compiler</c>,
/// and <c>Broiler.JavaScript.BuiltIns</c> assemblies are loaded before any test
/// code runs so that their module initializers register CLR interop, the
/// FastCompiler pipeline, and the BuiltIns type registrations.
/// </summary>
internal static class BuiltInsTestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureAssembliesLoaded()
    {
        // Force-load the Clr assembly and run its module initializer.
        _ = Broiler.JavaScript.Clr.DefaultClrInterop.Instance;
        // Force-load the Compiler assembly and run its module initializer.
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
        // Force-load the BuiltIns assembly and run its module initializer
        // by referencing a public type from the assembly.
        RuntimeHelpers.RunModuleConstructor(typeof(JSWeakRef).Module.ModuleHandle);
    }
}
