using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Disposable;

namespace Broiler.JavaScript.BuiltIns;

internal static class BuiltInsAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Register BuiltIns assembly types into the built-in registration pipeline.
        // This appends to any existing additional registrations so that multiple
        // satellite assemblies can contribute built-in types.
        var existing = DefaultBuiltInRegistry.AdditionalRegistrations;
        DefaultBuiltInRegistry.AdditionalRegistrations = existing == null
            ? static context => context.RegisterBuiltInClasses()
            : context =>
            {
                existing(context);
                context.RegisterBuiltInClasses();
            };

        // Wire factory delegate for JSDisposableStack so the Compiler can create
        // instances via the IJSDisposableStack interface without referencing BuiltIns.
        IJSDisposableStack.CreateNew = static () => new JSDisposableStack();
    }
}
