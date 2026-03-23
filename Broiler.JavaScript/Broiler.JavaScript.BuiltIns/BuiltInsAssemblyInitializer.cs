using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.BuiltIns.Debug;
using Broiler.JavaScript.BuiltIns.Decimal;
using Broiler.JavaScript.BuiltIns.Disposable;
using Broiler.JavaScript.BuiltIns.Intl;
using Broiler.JavaScript.BuiltIns.Iterator;
using Broiler.JavaScript.BuiltIns.Map;
using Broiler.JavaScript.BuiltIns.Set;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.BigInt;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Date;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.Core.Global;
using Broiler.JavaScript.Core.Core.Primitive;

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

        // Wire factory delegates for JSBigInt so Core/Compiler can create
        // BigInt values without referencing the concrete type directly.
        JSValue.CreateBigIntFromStringFactory = static s => new JSBigInt(s);
        JSValue.CreateBigIntFactory = static v => new JSBigInt(v);

        // Wire factory delegate for JSConsole so DefaultBuiltInRegistry
        // does not directly reference the concrete type.
        DefaultBuiltInRegistry.ConsoleFactory = static ctx => new JSConsole(ctx);

        // Wire structured clone extension for Map, Set, and ArrayBuffer types so that
        // JSGlobal.StructuredClone works without Core referencing BuiltIns.
        DefaultBuiltInRegistry.StructuredCloneExtension = static (value, seen, recurse) =>
        {
            if (value is JSMap map)
            {
                var clone = new JSMap(Arguments.Empty);
                seen[value] = clone;
                foreach (var entry in map.GetEntries())
                {
                    var clonedKey = recurse(entry[0], seen);
                    var clonedVal = recurse(entry[1], seen);
                    clone.Set(clonedKey, clonedVal);
                }
                return clone;
            }

            if (value is JSSet set)
            {
                var clone = new JSSet(Arguments.Empty);
                seen[value] = clone;
                foreach (var item in set.Keys())
                    clone.Add(recurse(item, seen));
                return clone;
            }

            if (value is JSArrayBuffer arrayBuffer)
            {
                if (arrayBuffer.isDetached)
                    throw JSContext.NewTypeError("structuredClone: cannot clone a detached ArrayBuffer");

                var newBuf = new byte[arrayBuffer.buffer.Length];
                System.Array.Copy(arrayBuffer.buffer, newBuf, arrayBuffer.buffer.Length);

                var clone = new JSArrayBuffer(newBuf);
                seen[value] = clone;
                return clone;
            }

            return null;
        };

        // Wire Iterator.prototype helper methods so DefaultBuiltInRegistry
        // does not directly reference JSIteratorObject.
        DefaultBuiltInRegistry.IteratorPrototypeSetup = static proto =>
        {
            DefaultBuiltInRegistry.AddProto(proto, "map", JSIteratorObject.StaticMap);
            DefaultBuiltInRegistry.AddProto(proto, "filter", JSIteratorObject.StaticFilter);
            DefaultBuiltInRegistry.AddProto(proto, "take", JSIteratorObject.StaticTake);
            DefaultBuiltInRegistry.AddProto(proto, "drop", JSIteratorObject.StaticDrop);
            DefaultBuiltInRegistry.AddProto(proto, "flatMap", JSIteratorObject.StaticFlatMap);
            DefaultBuiltInRegistry.AddProto(proto, "reduce", JSIteratorObject.StaticReduce);
            DefaultBuiltInRegistry.AddProto(proto, "toArray", JSIteratorObject.StaticToArray);
            DefaultBuiltInRegistry.AddProto(proto, "forEach", JSIteratorObject.StaticForEach);
            DefaultBuiltInRegistry.AddProto(proto, "some", JSIteratorObject.StaticSome);
            DefaultBuiltInRegistry.AddProto(proto, "every", JSIteratorObject.StaticEvery);
            DefaultBuiltInRegistry.AddProto(proto, "find", JSIteratorObject.StaticFind);
        };
    }
}
