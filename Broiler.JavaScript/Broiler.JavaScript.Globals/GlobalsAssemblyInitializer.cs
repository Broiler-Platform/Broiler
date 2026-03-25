using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns;

namespace Broiler.JavaScript.Globals;

internal static class GlobalsAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Register Globals assembly types into the built-in registration pipeline.
        // This appends to any existing additional registrations so that multiple
        // satellite assemblies can contribute built-in types.
        var existing = DefaultBuiltInRegistry.AdditionalRegistrations;
        DefaultBuiltInRegistry.AdditionalRegistrations = existing == null
            ? static context => context.RegisterGlobalClasses()
            : context =>
            {
                existing(context);
                context.RegisterGlobalClasses();
            };
    }
}
