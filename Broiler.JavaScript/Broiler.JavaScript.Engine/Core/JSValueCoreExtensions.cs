using System.Runtime.CompilerServices;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Engine.Core;

/// <summary>
/// Initializes <see cref="JSValue"/> factory delegates so that Runtime
/// types can create concrete JS values without referencing Core directly.
/// </summary>
internal static class JSValueCoreExtensions
{
    [ModuleInitializer]
    internal static void InitializeFactories()
    {
        JSValue.UndefinedValue = JSUndefined.Value;

        JSValue.NewTypeError = msg => JSEngine.NewTypeError(msg);
        JSValue.ForceConvertHelper = (jsValue, type, _) =>
        {
            var protoObj = (jsValue.prototypeChain as IJSPrototype)?.Object as JSObject;
            if (protoObj != null
                && JSEngine.ClrInterop.TryUnwrapClrObject(protoObj, out var clrObj))
            {
                if (((System.Type)type).IsAssignableFrom(clrObj.GetType()))
                    return clrObj;
            }
            return null;
        };
        JSValue.InvokePropertyGetter = (getter, receiver) => getter is IJSFunction fn ? fn.InvokeFunction(new Arguments(receiver)) : JSValue.UndefinedValue;
        JSValue.CreatePrototypeObject = value => (value as JSObject)?.PrototypeObject;
        Arguments.Empty = new Arguments(JSUndefined.Value);

        // Proactively load the BuiltIns assembly so that its ModuleInitializer
        // wires string/number/boolean factories (JSValue.CreateString, etc.)
        // before any user code runs—even without a JSContext being created.
        JSEngine.EnsureBuiltInsAssemblyLoaded();
    }
}
