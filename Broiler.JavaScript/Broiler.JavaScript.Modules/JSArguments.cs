using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Modules;

public class JSArguments: JSObject
{
    public static JSValue Callee(in Arguments a) => throw JSEngine.NewTypeError($"Cannot access callee in strict mode");

    public new JSValue Values(in Arguments a) => JSGeneratorBuilder.CreateFromEnumerator(GetElementEnumerator(), "Arguments");

    public static JSValue[] Empty = [];

    public override bool BooleanValue => true;

    public override JSValue TypeOf() => JSConstants.Arguments;

    internal override PropertyKey ToKey(bool create = false) => KeyStrings.arguments;

    public JSArguments(in Arguments args)
    {
        // arguments = args;
        ref var properties = ref GetOwnProperties(true);
        properties.Put(KeyStrings.length, JSValue.CreateNumber(args.Length), JSPropertyAttributes.ConfigurableValue);
        properties.Put(KeyStrings.callee, Callee, Callee, JSPropertyAttributes.Property);

        ref var symbols = ref GetSymbols();
        symbols.Put(JSValue.SymbolIterator.Key) = JSProperty.Property(new JSFunction(Values), JSPropertyAttributes.ConfigurableValue);
        ref var elements = ref CreateElements();
        
        for (int i = 0; i < args.Length; i++)
            elements.Put((uint)i, args.GetAt(i));
    }

    public override string ToString() => "[object Arguments]";
}
