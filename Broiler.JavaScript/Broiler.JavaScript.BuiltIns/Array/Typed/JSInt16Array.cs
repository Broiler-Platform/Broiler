using Broiler.JavaScript.ExpressionCompiler;
using System;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Array.Typed;

[JSClassGenerator("Int16Array"), JSBaseClass("TypedArray")]
public partial class JSInt16Array : JSTypedArray
{
    [JSExport("BYTES_PER_ELEMENT")]
    internal static readonly int BYTES_PER_ELENENT = 2;

    [JSExport(Length = 3)]
    public JSInt16Array(in Arguments a) : base(new TypedArrayParameters(a, BYTES_PER_ELENENT)) { }

    private JSInt16Array(TypedArrayParameters a) : base(a) { }

    public override JSValue GetValue(uint index, JSValue receiver, bool throwError = true)
    {
        if (index < 0 || index >= length)
            return JSUndefined.Value;
        return new JSNumber(BitConverter.ToInt16(buffer.buffer, byteOffset + (int)index * 2));
    }

    public override bool SetValue(uint index, JSValue value, JSValue receiver, bool throwError = true)
    {
        if (index < 0 || index >= length)
            return false;
        System.Array.Copy(BitConverter.GetBytes((short)value.IntValue), 0, buffer.buffer, byteOffset + index * 2, 2);
        return true;
    }

    [JSExport(Length = 1)]
    public static JSValue From(in Arguments a) => new JSInt16Array(TypedArrayParameters.From(a, BYTES_PER_ELENENT));

    [JSExport]
    public static JSValue Of(in Arguments a)
    {
        var r = new JSInt16Array(TypedArrayParameters.Of(in a, BYTES_PER_ELENENT));
        for (int i = 0; i < a.Length; i++)
        {
            r[(uint)i] = a[i];
        }
        return r;
    }
}
