using System;
using Broiler.JavaScript.Core.Core.Debug;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.Core.Core.Iterator;
using Broiler.JavaScript.Core.Core.Storage;

namespace Broiler.JavaScript.Core.Core;

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

    /// <inheritdoc />
    public void Register(JSContext context)
    {
        context.RegisterGeneratedClasses();

        // Register built-in types from satellite assemblies.
        AdditionalRegistrations?.Invoke(context);

        // Set up Iterator.prototype helpers and prototype chain (ES2025).
        SetupIteratorPrototypeChain(context);

        // Register the console object.
        context[KeyStrings.console] = JSContext.ClrInterop.Marshal(new JSConsole(context));
    }

    private static void SetupIteratorPrototypeChain(JSContext context)
    {
        if (context[Names.Iterator] is not JSFunction iteratorCtor)
            return;

        var proto = iteratorCtor.prototype;

        // Iterator.prototype[Symbol.iterator] returns `this`.
        ref var symbols = ref proto.GetSymbols();
        symbols.Put(JSSymbol.iterator.Key) = JSProperty.Property(new JSFunction((in Arguments a) => a.This, "Symbol.iterator"), JSPropertyAttributes.ConfigurableValue);

        // Register prototype helper methods so they work on any iterator
        // (generators, user iterators, etc.) — not just JSIteratorObject.
        AddProto(proto, "map", JSIteratorObject.StaticMap);
        AddProto(proto, "filter", JSIteratorObject.StaticFilter);
        AddProto(proto, "take", JSIteratorObject.StaticTake);
        AddProto(proto, "drop", JSIteratorObject.StaticDrop);
        AddProto(proto, "flatMap", JSIteratorObject.StaticFlatMap);
        AddProto(proto, "reduce", JSIteratorObject.StaticReduce);
        AddProto(proto, "toArray", JSIteratorObject.StaticToArray);
        AddProto(proto, "forEach", JSIteratorObject.StaticForEach);
        AddProto(proto, "some", JSIteratorObject.StaticSome);
        AddProto(proto, "every", JSIteratorObject.StaticEvery);
        AddProto(proto, "find", JSIteratorObject.StaticFind);

        // Generator.prototype → Iterator.prototype (§2.1.14).
        if (context[Names.Generator] is JSFunction generatorCtor)
            generatorCtor.prototype.SetPrototypeOf(proto);
    }

    private static void AddProto(JSObject proto, string name, JSFunctionDelegate fn)
    {
        proto.FastAddValue(KeyStrings.GetOrCreate(name), new JSFunction(fn, name, $"function {name}() {{ [native] }}", createPrototype: false), JSPropertyAttributes.ConfigurableValue);
    }
}
