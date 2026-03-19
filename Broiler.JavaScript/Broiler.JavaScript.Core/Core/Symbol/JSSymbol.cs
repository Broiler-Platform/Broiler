using Broiler.JavaScript.Core.Core;
using System;
using System.Threading;
using Yantra.Core;

namespace YantraJS.Core;

[JSBaseClass("Object")]
[JSFunctionGenerator("Symbol")]
public partial class JSSymbol: JSValue
{
    private static int SymbolID = 1;
    private readonly string name;
    public readonly uint Key;

    public override bool BooleanValue => true;

    public override bool IsSymbol => true;

    public override double DoubleValue => throw JSContext.NewTypeError("Cannot convert a Symbol value to a number.");

    internal override string StringValue => throw JSContext.NewTypeError("Cannot convert a Symbol value to a string.");

    public override uint UIntValue => throw JSContext.NewTypeError("Cannot convert a Symbol value to a uint32.");

    internal override PropertyKey ToKey(bool create = true) => this;

    public JSSymbol(string name) : base(JSContext.Current.ObjectPrototype)
    {
        this.name = name;
        Key = (uint)Interlocked.Increment(ref SymbolID);
    }

    public override JSValue TypeOf() => JSConstants.Symbol;

    public override bool Equals(object obj)
    {
        if (obj is JSSymbol s)
            return s.Key == Key;

        return false;
    }

    public override bool Equals(JSValue value) => ReferenceEquals(this, value);
    public override int GetHashCode() => (int)Key;

    public override JSValue InvokeFunction(in Arguments a)
    {
        var f = a.Get1();
        if (f.IsUndefined)
            return new JSSymbol("");

        return new JSSymbol(a.ToString());
    }

    public override JSValue CreateInstance(in Arguments a) => throw new NotSupportedException();

    public override bool StrictEquals(JSValue value) => ReferenceEquals(this, value);

    public override string ToString() => name;
}
