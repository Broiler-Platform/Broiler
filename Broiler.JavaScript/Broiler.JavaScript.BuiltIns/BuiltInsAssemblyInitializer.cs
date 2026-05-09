using System.Runtime.CompilerServices;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.BuiltIns.Date;
using Broiler.JavaScript.BuiltIns.Debug;
using Broiler.JavaScript.BuiltIns.Decimal;
using Broiler.JavaScript.BuiltIns.Disposable;
using Broiler.JavaScript.BuiltIns.Error;
using Broiler.JavaScript.BuiltIns.Intl;
using Broiler.JavaScript.BuiltIns.Iterator;
using Broiler.JavaScript.BuiltIns.Map;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Promise;
using Broiler.JavaScript.BuiltIns.Set;
using Broiler.JavaScript.BuiltIns.Symbol;
using Broiler.JavaScript.BuiltIns.BigInt;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Generator;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions;
using Broiler.JavaScript.BuiltIns.Class;
using Broiler.JavaScript.BuiltIns.RegExp;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns;

internal static class BuiltInsAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Set the default built-in registry on JSContext.
        // DefaultBuiltInRegistry now lives in this assembly (BuiltIns).
        JSEngine.BuiltInRegistry ??= DefaultBuiltInRegistry.Instance;

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
        DefaultBuiltInRegistry.IntlFactory = static () => JSEngine.ClrInterop.GetClrType(typeof(JSIntl));

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

        // Initialize JSFunctionBuilder with the concrete JSFunction type so the
        // Compiler can build function expression trees without a direct reference.
        JSFunctionBuilder.Initialize(typeof(JSFunction));

        // Initialize JSRegExpBuilder with the concrete JSRegExp type so the
        // Compiler can build regex expression trees without a direct reference.
        JSRegExpBuilder.Initialize(typeof(JSRegExp));

        // Wire factory delegates for JSFunction so Core can create
        // function instances without referencing the concrete type directly.
        JSValue.CreateFunctionFactory = static d => new JSFunction(d);
        JSValue.CreateFunctionFullFactory = static (d, name, source, length, createProto) =>
            new JSFunction(d, name, source, length, createProto);

        // Wire factory delegate for JSFunction.CreateClass so JSContext can
        // build the Function constructor without referencing JSFunction directly.
        JSEngine.CreateFunctionClass = static (ctx, register) => JSFunction.CreateClass(ctx, register);

        // Wire factory delegates for JSGenerator so Core and Clr can create
        // generator instances without a direct type reference.
        JSGeneratorBuilder.CreateFromEnumerator = static (en, name) => new JSGenerator(en, name);
        JSGeneratorBuilder.CreateFromClrV2 = static g => new JSGenerator((ClrGeneratorV2)g);

        // Wire factory delegate for JSPrototype so Core can create prototype
        // instances without referencing the concrete type directly.
        JSObject.CreatePrototype = static obj => new JSPrototype(obj);

        // Wire factory delegates for JSError types so Core can create
        // error instances without referencing the concrete types directly.
        JSEngine.CreateTypeError = static (message, function, filePath, line) =>
            new JSTypeError(new Arguments(JSUndefined.Value, JSValue.CreateString(message)), function: function, filePath: filePath, line: line).Exception;
        JSEngine.CreateSyntaxError = static (message, function, filePath, line) =>
            new JSSyntaxError(new Arguments(JSUndefined.Value, JSValue.CreateString(message)), function: function, filePath: filePath, line: line).Exception;
        JSEngine.CreateURIError = static (message, function, filePath, line) =>
            new JSURIError(new Arguments(JSUndefined.Value, JSValue.CreateString(message)), function: function, filePath: filePath, line: line).Exception;
        JSEngine.CreateRangeError = static (message, function, filePath, line) =>
            new JSRangeError(new Arguments(JSUndefined.Value, JSValue.CreateString(message)), function: function, filePath: filePath, line: line).Exception;
        JSEngine.CreateError = static (message, function, filePath, line) =>
            new JSError(new Arguments(JSUndefined.Value, JSValue.CreateString(message)), function: function, filePath: filePath, line: line).Exception;
        JSException.CreateJSError = static (ex, msg) => new JSError(ex, msg);
        JSException.CreateJSErrorWithPrototype = static (ex, prototype) => new JSError(ex, prototype);
        JSException.JSErrorFrom = static (ex) => JSError.From(ex);

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
                    throw JSEngine.NewTypeError("structuredClone: cannot clone a detached ArrayBuffer");

                var newBuf = new byte[arrayBuffer.buffer.Length];
                System.Array.Copy(arrayBuffer.buffer, newBuf, arrayBuffer.buffer.Length);

                var clone = new JSArrayBuffer(newBuf);
                seen[value] = clone;
                return clone;
            }

            if (value is JSTypedArray typedArray)
            {
                var clonedBuffer = recurse(typedArray.buffer, seen) as JSArrayBuffer
                    ?? throw JSEngine.NewTypeError("structuredClone: typed array buffer must be an ArrayBuffer");

                var clone = CloneTypedArray(typedArray, clonedBuffer);
                seen[value] = clone;
                return clone;
            }

            if (value is Broiler.JavaScript.BuiltIns.DataView.DataView dataView)
            {
                var clonedBuffer = recurse(dataView.Buffer, seen) as JSArrayBuffer
                    ?? throw JSEngine.NewTypeError("structuredClone: DataView buffer must be an ArrayBuffer");

                var clone = new Broiler.JavaScript.BuiltIns.DataView.DataView(clonedBuffer, dataView.ByteOffset, dataView.ByteLength);
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

        // Wire factory delegates for JSPromise so Core can create
        // promise instances without referencing the concrete type directly.
        JSEngine.CreateResolvedOrRejectedPromise = static (value, isResolved) =>
            new JSPromise(value, isResolved ? JSPromise.PromiseState.Resolved : JSPromise.PromiseState.Rejected);
        JSEngine.CreatePromiseFromDelegate = static (d) => new JSPromise(d);
        JSValue.CreatePromiseFromTask = static (task) => new JSPromise(task);
        JSValue.CreatePromiseFromUntypedTask = static (task) => task.ToPromise();
        JSValue.CreatePromiseFromGenericTask = static (task) => task.ToPromise();

        // Wire JSFunction.CreateClrDelegateFactory (moved from LinqExpressionsAssemblyInitializer)
        JSFunction.CreateClrDelegateFactory = LinqExpressionsAssemblyInitializer.CreateClrDelegate;

        // Initialize builders for generator/async function types
        JSGeneratorFunctionBuilderV2.Initialize(typeof(JSGeneratorFunctionV2));
        JSAsyncFunctionBuilder.Initialize(typeof(JSAsyncFunction), typeof(JSValue));
    }

    private static JSTypedArray CloneTypedArray(JSTypedArray typedArray, JSArrayBuffer clonedBuffer)
    {
        var args = new Arguments(
            JSUndefined.Value,
            clonedBuffer,
            new JSNumber(typedArray.byteOffset),
            new JSNumber(typedArray.Length * typedArray.bytesPerElement));

        return typedArray switch
        {
            JSInt8Array => new JSInt8Array(args),
            JSUInt8Array => new JSUInt8Array(args),
            JSUint8ClampedArray => new JSUint8ClampedArray(args),
            JSInt16Array => new JSInt16Array(args),
            JSUInt16Array => new JSUInt16Array(args),
            JSInt32Array => new JSInt32Array(args),
            JSUInt32Array => new JSUInt32Array(args),
            JSFloat16Array => new JSFloat16Array(args),
            JSFloat32Array => new JSFloat32Array(args),
            JSFloat64Array => new JSFloat64Array(args),
            _ => throw JSEngine.NewTypeError($"structuredClone: unsupported typed array type {typedArray.GetType().Name}")
        };
    }
}
