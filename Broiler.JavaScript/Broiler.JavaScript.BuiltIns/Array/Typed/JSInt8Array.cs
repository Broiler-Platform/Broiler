using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Array.Typed;

[JSClassGenerator("Int8Array"), JSBaseClass("TypedArray")]
public partial class JSInt8Array : JSTypedArray
{
    [JSExport("BYTES_PER_ELEMENT")]
    internal static readonly int BYTES_PER_ELENENT = 1;

    [JSExport(Length = 3)]
    public JSInt8Array(in Arguments a) : base(new TypedArrayParameters(a, BYTES_PER_ELENENT)) { }

    private JSInt8Array(TypedArrayParameters a) : base(a) { }

    public override JSValue GetValue(uint index, JSValue receiver, bool throwError = true)
    {
        if (index < 0 || index >= length)
            return JSUndefined.Value;
        return new JSNumber((sbyte)buffer.buffer[byteOffset + index]);
    }

    public override bool SetValue(uint index, JSValue value, JSValue receiver, bool throwError = true)
    {
        if (index < 0 || index >= length)
            return false;
        buffer.buffer[byteOffset + index] = (byte)value.IntValue;
        return true;
    }

    [JSExport(Length = 1)]
    public static JSValue From(in Arguments a) => new JSInt8Array(TypedArrayParameters.From(in a, BYTES_PER_ELENENT));

    [JSExport]
    public static JSValue Of(in Arguments a)
    {
        var r = new JSInt8Array(TypedArrayParameters.Of(in a, BYTES_PER_ELENENT));
        for (int i = 0; i < a.Length; i++)
        {
            r[(uint)i] = a[i];
        }
        return r;
    }
}
