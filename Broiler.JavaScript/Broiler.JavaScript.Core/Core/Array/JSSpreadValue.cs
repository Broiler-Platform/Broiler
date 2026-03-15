using System;

namespace YantraJS.Core.Core.Array;

public class JSSpreadValue(JSValue value) : JSValue(null)
{
    private int _length = value.Length;

    internal override bool IsSpread => true;

    public override int Length { get => _length; set { } }

    public override JSValue this[uint key] { 
        get => Value[key]; 
        set => Value[key] = value; 
    }


    public override bool BooleanValue => throw new NotImplementedException();

    public JSValue Value { get; } = value;

    public override bool Equals(JSValue value) => throw new NotImplementedException();

    public override JSValue InvokeFunction(in Arguments a) => throw new NotImplementedException();

    public override bool StrictEquals(JSValue value) => throw new NotImplementedException();

    public override JSValue TypeOf() => throw new NotImplementedException();

    internal override PropertyKey ToKey(bool create = true) => throw new NotImplementedException();
}
