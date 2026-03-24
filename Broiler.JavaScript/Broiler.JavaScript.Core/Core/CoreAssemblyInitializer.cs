using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.Core;

internal static class CoreAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        JSEngine.CoreClassRegistrations = static ctx => ctx.RegisterGeneratedClasses();

        // Wire JSObject factory delegates for Core dependencies
        JSObject.NewTypeError = static msg => JSEngine.NewTypeError(msg);
        JSObject.GetCurrentObjectPrototype = static () => JSEngine.Current?.ObjectPrototype;
        JSObject.CoerceToNumber = static str => Utils.NumberParser.CoerceToNumber(str);
        JSObject.CreatePrimitiveObject = static p => new Primitive.JSPrimitiveObject(p);
        JSObject.TryGetClrEnumeratorFunc = Internal.CoreInternalHelpers.TryGetClrEnumerator;
        JSObject.TryUnmarshalObject = Internal.CoreInternalHelpers.TryUnmarshal;
    }
}
