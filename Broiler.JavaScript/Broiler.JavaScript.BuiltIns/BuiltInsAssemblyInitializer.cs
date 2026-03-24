using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.BuiltIns.Date;
using Broiler.JavaScript.BuiltIns.Debug;
using Broiler.JavaScript.BuiltIns.Decimal;
using Broiler.JavaScript.BuiltIns.Disposable;
using Broiler.JavaScript.BuiltIns.Intl;
using Broiler.JavaScript.BuiltIns.Iterator;
using Broiler.JavaScript.BuiltIns.Map;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Set;
using Broiler.JavaScript.BuiltIns.Symbol;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.BuiltIns.BigInt;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Generator;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.BuiltIns.Class;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Runtime;

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

        // Wire factory delegate for the Intl global object so the Globals assembly
        // does not directly reference JSIntl.
        DefaultBuiltInRegistry.IntlFactory = static () => JSContext.ClrInterop.GetClrType(typeof(JSIntl));

        // Wire factory delegate for JSDate so Core/Clr can create
        // Date values without referencing the concrete type directly.
        JSValue.CreateDateFactory = static v => new JSDate(v);

        // Wire factory delegates for JSArray so Core can create
        // array values without referencing the concrete type directly.
        JSValue.CreateArrayFactory = static () => new JSArray();
        JSValue.CreateArrayWithLengthFactory = static count => new JSArray(count);

        // Initialize JSArrayBuilder with the concrete JSArray type so the
        // Compiler can build array expression trees without a direct reference.
        JSArrayBuilder.Initialize(typeof(JSArray));

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

        // Wire JSNumber singletons and factory delegates so Core/Compiler can
        // create and inspect number values without referencing the concrete type directly.
        JSValue.NumberOne = JSNumber.One;
        JSValue.NumberNaN = JSNumber.NaN;
        JSValue.NumberZero = JSNumber.Zero;
        JSValue.NumberMinusOne = JSNumber.MinusOne;
        JSValue.NumberTwo = JSNumber.Two;
        JSValue.NumberNegativeZero = JSNumber.NegativeZero;
        JSValue.NumberPositiveInfinity = JSNumber.PositiveInfinity;
        JSValue.NumberNegativeInfinity = JSNumber.NegativeInfinity;
        JSValue.CreateNumber = static v => new JSNumber(v);
        JSValue.NumberToECMAString = JSNumber.ToECMAString;
        JSValue.IsPositiveZeroCheck = JSNumber.IsPositiveZero;
        JSValue.IsNegativeZeroCheck = JSNumber.IsNegativeZero;

        // Initialize JSNumberBuilder with the concrete JSNumber type so the
        // Compiler can build number expression trees without a direct reference.
        JSNumberBuilder.Initialize(typeof(JSNumber));

        // Wire JSBoolean singletons so Core/Runtime can access boolean
        // values without referencing the concrete type directly.
        JSValue.BooleanTrue = JSBoolean.True;
        JSValue.BooleanFalse = JSBoolean.False;

        // Wire JSNull singleton so Core/Runtime can access the null
        // value without referencing the concrete type directly.
        JSValue.NullValue = JSNull.Value;
        JSNullBuilder.Initialize(typeof(JSNull));

        // Wire JSString factory delegates and cached empty-string value
        // so Core/Runtime can create string values without referencing
        // the concrete type directly.
        JSValue.CreateString = static v => new JSString(v);
        JSValue.EmptyString = JSString.Empty;
        JSValue.CreateStringWithKey = static (s, k) => new JSString(s, k);

        // Initialize JSStringBuilder with the concrete JSString type so the
        // Compiler can build string expression trees without a direct reference.
        JSStringBuilder.Initialize(typeof(JSString));

        // Wire JSSymbol well-known singletons and factory delegates so Core
        // and other assemblies can work with symbols without referencing the
        // concrete JSSymbol type directly.
        JSValue.SymbolIterator = JSSymbol.iterator;
        JSValue.SymbolDispose = JSSymbol.dispose;
        JSValue.SymbolAsyncDispose = JSSymbol.asyncDispose;
        JSValue.CreateSymbolFactory = static name => new JSSymbol(name);
        JSValue.CreateSymbolClassFactory = static (ctx, register) =>
            JSSymbol.CreateClass((JSContext)ctx, register);
        JSValue.GetGlobalSymbolFactory = static name => JSSymbol.GlobalSymbol(name);

        // Initialize JSSymbolBuilder with the concrete JSSymbol type so the
        // ClassGenerator can emit symbol lookups without a direct reference.
        JSSymbolBuilder.Initialize(typeof(JSSymbol));

        // Initialize JSClassBuilder with the concrete JSClass type so the
        // Compiler can build class expression trees without a direct reference.
        JSClassBuilder.Initialize(typeof(JSClass), typeof(JSFunction), typeof(JSFunctionDelegate));

        // Wire factory delegates for JSGenerator so Core and Clr can create
        // generator instances without a direct type reference.
        JSGeneratorBuilder.CreateFromEnumerator = static (en, name) => new JSGenerator(en, name);
        JSGeneratorBuilder.CreateFromClrV2 = static g => new JSGenerator(g);

        // Wire factory delegate for JSPrototype so Core can create prototype
        // instances without referencing the concrete type directly.
        JSObject.CreatePrototype = static obj => new JSPrototype(obj);

        // Wire JSConstants with concrete JSString instances.
        JSConstants.Decimal = new JSString("decimal");
        JSConstants.Arguments = new JSString("arguments");
        JSConstants.BigInt = new JSString("bigint");
        JSConstants.Undefined = new JSString("undefined");
        JSConstants.Boolean = new JSString("boolean");
        JSConstants.String = new JSString("string");
        JSConstants.Object = new JSString("object");
        JSConstants.Number = new JSString("number");
        JSConstants.Function = new JSString("function");
        JSConstants.Symbol = new JSString("symbol");
        JSConstants.Infinity = new JSString("Infinity");
        JSConstants.NegativeInfinity = new JSString("-Infinity");

        // Wire factory delegate for JSConsole so DefaultBuiltInRegistry
        // does not directly reference the concrete type.
        DefaultBuiltInRegistry.ConsoleFactory = static ctx => new JSConsole(ctx);

        // Wire structured clone extension for Date, Map, Set, and ArrayBuffer types so that
        // JSGlobal.StructuredClone works without Core referencing BuiltIns.
        DefaultBuiltInRegistry.StructuredCloneExtension = static (value, seen, recurse) =>
        {
            if (value is JSDate date)
            {
                var clone = new JSDate(date.value);
                seen[value] = clone;
                return clone;
            }

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
