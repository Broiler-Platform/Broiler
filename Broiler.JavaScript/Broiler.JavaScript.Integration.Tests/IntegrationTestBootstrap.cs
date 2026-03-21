using System.Runtime.CompilerServices;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Core.Core.Weak;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Ensures all satellite assemblies (Clr, Compiler, BuiltIns) are loaded and
/// their module initializers have executed before any integration test runs.
/// This mirrors the bootstrap pattern used by other test projects (e.g.,
/// BuiltIns.Tests) but includes every satellite assembly to test cross-assembly
/// wiring.
/// </summary>
internal static class IntegrationTestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureAssembliesLoaded()
    {
        // Force-load the Clr assembly and run its module initializer.
        _ = DefaultClrInterop.Instance;
        // Force-load the Compiler assembly and run its module initializer.
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
        // Force-load the BuiltIns assembly and run its module initializer.
        RuntimeHelpers.RunModuleConstructor(typeof(JSWeakRef).Module.ModuleHandle);
    }
}
