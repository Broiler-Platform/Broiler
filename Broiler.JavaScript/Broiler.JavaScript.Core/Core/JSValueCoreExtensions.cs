using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Primitive;

namespace Broiler.JavaScript.Core.Core;

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

        JSValue.CreateString = v => new JSString(v);
        JSValue.NewTypeError = msg => JSContext.NewTypeError(msg);
        JSValue.CreateDynamicMetaObject = (param, value) => new JSDynamicMetaData(param, value);
        JSValue.ForceConvertHelper = (jsValue, type, _) =>
        {
            var protoObj = (jsValue.prototypeChain as JSPrototype)?.@object;
            if (protoObj != null
                && JSContext.ClrInterop.TryUnwrapClrObject(protoObj, out var clrObj))
            {
                if (((System.Type)type).IsAssignableFrom(clrObj.GetType()))
                    return clrObj;
            }
            return null;
        };
        JSValue.InvokePropertyGetter = (getter, receiver) => ((JSFunction)getter).InvokeFunction(new Arguments(receiver));
        JSValue.CreatePrototypeObject = value => (value as JSObject)?.PrototypeObject;
        Arguments.Empty = new Arguments(JSUndefined.Value);
        Arguments.ForApplyImpl = ArgumentsCoreExtensions.ForApplyCore;
        Arguments.RestFromImpl = ArgumentsCoreExtensions.RestFromCore;
        Arguments.GetStringImpl = ArgumentsCoreExtensions.GetStringCore;
        Arguments.GetSpreadTarget = ArgumentsCoreExtensions.GetSpreadTargetCore;
    }
}
