using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.Core;

/// <summary>
/// Wires Core-specific delegates during assembly initialization.
/// This runs before any <see cref="JSContext"/> is constructed, ensuring
/// that the Core source-generated registration method is available to
/// the registry implementation in the BuiltIns assembly.
/// </summary>
internal static class CoreAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Expose Core's source-generated RegisterGeneratedClasses as a
        // delegate so that the DefaultBuiltInRegistry (now in BuiltIns)
        // can invoke it without a direct reference to the internal Names class.
        JSContext.CoreClassRegistrations = static ctx => ctx.RegisterGeneratedClasses();
    }
}
