using System;
using System.Collections.Generic;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

// Moved from Broiler.JavaScript.Core to Broiler.JavaScript.BuiltIns.
// Rationale: DefaultBuiltInRegistry is responsible for orchestrating the
// registration of built-in JavaScript objects (Array, String, Number, etc.)
// and logically belongs with the built-in type implementations. The assembly-
// loading bootstrap logic was extracted to JSContext so that Core can load
// satellite assemblies without a circular dependency.

namespace Broiler.JavaScript.BuiltIns;

/// <summary>
/// Default implementation of <see cref="IBuiltInRegistry"/> that delegates
/// to the source-generated <c>Names.RegisterGeneratedClasses</c> method.
/// This preserves the existing registration behavior where every built-in
/// object decorated with <c>[JSFunctionGenerator]</c> /
/// <c>[JSClassGenerator]</c> is registered automatically.
/// </summary>
public sealed class DefaultBuiltInRegistry : IBuiltInRegistry
{
    /// <summary>
    /// Shared singleton instance — the default registry is stateless.
    /// </summary>
    public static readonly DefaultBuiltInRegistry Instance = new();

    /// <summary>
    /// Optional delegate invoked after Core's generated classes are registered.
    /// Satellite assemblies (e.g., <c>Broiler.JavaScript.BuiltIns</c>) append
    /// their own registrations via their module initializer so that all
    /// built-in types are available when a <see cref="JSContext"/> is created.
    /// </summary>
    public static Action<JSContext> AdditionalRegistrations { get; set; }

    /// <summary>
    /// Factory delegate for creating the console object.
    /// Wired by the BuiltIns assembly's module initializer so that Core
    /// does not directly reference the concrete JSConsole type.
    /// </summary>
    public static Func<JSContext, object> ConsoleFactory { get; set; }

    /// <summary>
    /// Extension delegate for structured clone support of satellite assembly types
    /// (e.g., Map, Set, ArrayBuffer). Returns a cloned value, or null if the type
    /// is not handled. The third parameter is the recursive clone function.
    /// </summary>
    public static Func<JSValue, Dictionary<JSValue, JSValue>, Func<JSValue, Dictionary<JSValue, JSValue>, JSValue>, JSValue> StructuredCloneExtension { get; set; }

    /// <summary>
    /// Factory delegate for creating the Intl global object.
    /// Wired by the BuiltIns assembly's module initializer so that the
    /// Globals assembly does not directly reference JSIntl.
    /// </summary>
    public static Func<JSValue> IntlFactory { get; set; }

    /// <summary>
    /// Delegate for registering Iterator.prototype helper methods (map, filter,
    /// take, drop, flatMap, reduce, toArray, forEach, some, every, find).
    /// Wired by the BuiltIns assembly's module initializer so that Core does not
    /// directly reference the concrete JSIteratorObject type.
    /// </summary>
    public static Action<JSObject> IteratorPrototypeSetup { get; set; }

    /// <inheritdoc />
    public void Register(IJSContext ctx)
    {
        JSEngine.EnsureBuiltInsAssemblyLoaded();

        var context = ctx as JSContext
            ?? throw new ArgumentException("Expected JSContext instance", nameof(ctx));

        // Invoke Core's source-generated class registration via the delegate
        // wired by CoreAssemblyInitializer. This replaces the direct call to
        // context.RegisterGeneratedClasses() which is internal to Core.
        JSEngine.CoreClassRegistrations?.Invoke(context);

        // Register built-in types from satellite assemblies.
        AdditionalRegistrations?.Invoke(context);
        ApplyExperimentalFeatureFlags(context);

        // Set up Iterator.prototype helpers and prototype chain (ES2025).
        SetupIteratorPrototypeChain(context);

        // Register the console object via factory delegate (wired by BuiltIns assembly).
        if (ConsoleFactory != null)
            context[KeyStrings.console] = JSEngine.ClrInterop.Marshal(ConsoleFactory(context));
    }

    private static void ApplyExperimentalFeatureFlags(JSContext context)
    {
        if (!context.HasExperimentalFeature(JavaScriptFeatureFlags.StructuredClone))
            context.Delete(KeyStrings.GetOrCreate("structuredClone"));

        if (!context.HasExperimentalFeature(JavaScriptFeatureFlags.MathSumPrecise) &&
            context[KeyStrings.GetOrCreate("Math")] is JSObject mathObject)
        {
            mathObject.Delete(KeyStrings.GetOrCreate("sumPrecise"));
        }

        if (context[KeyStrings.GetOrCreate("Uint8Array")] is JSFunction uint8ArrayCtor &&
            !context.HasExperimentalFeature(JavaScriptFeatureFlags.Uint8ArrayBase64))
        {
            uint8ArrayCtor.Delete(KeyStrings.GetOrCreate("fromBase64"));
            uint8ArrayCtor.Delete(KeyStrings.GetOrCreate("fromHex"));
            uint8ArrayCtor.prototype.Delete(KeyStrings.GetOrCreate("toBase64"));
            uint8ArrayCtor.prototype.Delete(KeyStrings.GetOrCreate("toHex"));
            uint8ArrayCtor.prototype.Delete(KeyStrings.GetOrCreate("setFromBase64"));
            uint8ArrayCtor.prototype.Delete(KeyStrings.GetOrCreate("setFromHex"));
        }

        if (!context.HasExperimentalFeature(JavaScriptFeatureFlags.MapUpsert))
        {
            if (context[KeyStrings.GetOrCreate("Map")] is JSFunction mapCtor)
            {
                mapCtor.prototype.Delete(KeyStrings.GetOrCreate("getOrInsert"));
                mapCtor.prototype.Delete(KeyStrings.GetOrCreate("getOrInsertComputed"));
            }

            if (context[KeyStrings.GetOrCreate("WeakMap")] is JSFunction weakMapCtor)
            {
                weakMapCtor.prototype.Delete(KeyStrings.GetOrCreate("getOrInsert"));
                weakMapCtor.prototype.Delete(KeyStrings.GetOrCreate("getOrInsertComputed"));
            }
        }

        if (context[KeyStrings.GetOrCreate("Error")] is JSFunction errorCtor &&
            !context.HasExperimentalFeature(JavaScriptFeatureFlags.ErrorIsError))
        {
            errorCtor.Delete(KeyStrings.GetOrCreate("isError"));
        }

        if (context[KeyStrings.GetOrCreate("Array")] is JSFunction arrayCtor &&
            !context.HasExperimentalFeature(JavaScriptFeatureFlags.ArrayFromAsync))
        {
            arrayCtor.Delete(KeyStrings.GetOrCreate("fromAsync"));
        }

        if (!context.HasExperimentalFeature(JavaScriptFeatureFlags.ObjectMapGroupBy))
        {
            if (context[KeyStrings.GetOrCreate("Object")] is JSFunction objectCtor)
                objectCtor.Delete(KeyStrings.GetOrCreate("groupBy"));

            if (context[KeyStrings.GetOrCreate("Map")] is JSFunction mapCtor)
                mapCtor.Delete(KeyStrings.GetOrCreate("groupBy"));
        }

        if (context[KeyStrings.GetOrCreate("Iterator")] is JSFunction iteratorCtor &&
            !context.HasExperimentalFeature(JavaScriptFeatureFlags.IteratorConcat))
        {
            iteratorCtor.Delete(KeyStrings.GetOrCreate("concat"));
        }
    }

    private static void SetupIteratorPrototypeChain(JSContext context)
    {
        if (context[KeyStrings.GetOrCreate("Iterator")] is not JSFunction iteratorCtor)
            return;

        var proto = iteratorCtor.prototype;

        // Iterator.prototype[Symbol.iterator] returns `this`.
        ref var symbols = ref proto.GetSymbols();
        symbols.Put(JSValue.SymbolIterator.Key) = JSProperty.Property(new JSFunction((in Arguments a) => a.This, "Symbol.iterator"), JSPropertyAttributes.ConfigurableValue);

        // Register prototype helper methods via delegate (wired by BuiltIns assembly)
        // so they work on any iterator (generators, user iterators, etc.).
        IteratorPrototypeSetup?.Invoke(proto);

        // Generator.prototype → Iterator.prototype (§2.1.14).
        if (context[KeyStrings.GetOrCreate("Generator")] is JSFunction generatorCtor)
            generatorCtor.prototype.SetPrototypeOf(proto);

        if (context[KeyStrings.GetOrCreate("Set")] is JSFunction setCtor)
        {
            var values = setCtor.prototype[KeyStrings.GetOrCreate("values")];
            if (values.IsFunction)
            {
                ref var setSymbols = ref setCtor.prototype.GetSymbols();
                setSymbols.Put(JSValue.SymbolIterator.Key) = JSProperty.Property(values, JSPropertyAttributes.ConfigurableValue);
            }
        }
    }

    /// <summary>
    /// Registers a function as a configurable-value property on a prototype object.
    /// Used by satellite assemblies to add prototype methods.
    /// </summary>
    public static void AddProto(JSObject proto, string name, JSFunctionDelegate fn)
    {
        proto.FastAddValue(KeyStrings.GetOrCreate(name), new JSFunction(fn, name, $"function {name}() {{ [native] }}", createPrototype: false), JSPropertyAttributes.ConfigurableValue);
    }
}
