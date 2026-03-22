using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Date;
using Broiler.JavaScript.Core.Core.Debug;
using Broiler.JavaScript.Core.Core.Decimal;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.Core.Global;
using Broiler.JavaScript.Core.Core.Intl;

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

        // Wire factory delegate for the Intl global object so JSGlobalStatic
        // does not directly reference JSIntl.
        JSGlobalStatic.IntlFactory = static () => JSContext.ClrInterop.GetClrType(typeof(JSIntl));

        // Wire factory delegate for Intl date formatting so JSDatePrototype
        // does not directly reference JSIntlDateTimeFormat.
        JSDate.IntlDateFormatter = static (culture, value, options) =>
            JSIntlDateTimeFormat.Get(culture).Format(value, options);

        // Wire factory delegates for JSDecimal so Core/Compiler can create
        // and inspect decimal values without referencing the concrete type.
        JSValue.CreateDecimalFactory = static v => new JSDecimal(v);
        JSValue.CreateDecimalFromStringFactory = static s => new JSDecimal(s);

        // Wire factory delegate for JSConsole so DefaultBuiltInRegistry
        // does not directly reference the concrete type.
        DefaultBuiltInRegistry.ConsoleFactory = static ctx => new JSConsole(ctx);
    }
}
