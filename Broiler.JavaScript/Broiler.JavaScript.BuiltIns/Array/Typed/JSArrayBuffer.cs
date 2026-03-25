using System;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Extensions;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Array.Typed;

[JSClassGenerator("ArrayBuffer")]
public partial class JSArrayBuffer : JSObject
{
    internal byte[] buffer;
    internal bool isDetached;

    public byte[] Buffer => buffer;

    public JSArrayBuffer(in Arguments a) : this(JSEngine.NewTargetPrototype)
    {
        int length = a.Get1().AsInt32OrDefault();
        if (length < 0 || length > JSNumber.MaxSafeInteger)
        {
            throw JSEngine.NewRangeError("Buffer length out of range");
        }
        buffer = new byte[length];
    }

    public JSArrayBuffer(int length) : this() => buffer = new byte[length];
    public JSArrayBuffer(byte[] buffer) : this() => this.buffer = buffer;

    public override bool BooleanValue => true;

    public override double DoubleValue => double.NaN;

    public override bool Equals(JSValue value) => ReferenceEquals(this, value);

    public override JSValue InvokeFunction(in Arguments a) => throw JSEngine.NewTypeError($"{this} is not a function");

    public override bool StrictEquals(JSValue value) => ReferenceEquals(this, value);

    // ---------------------------------------------------------------
    // §2.9  ArrayBuffer.prototype.byteLength (getter)
    // ---------------------------------------------------------------

    [JSExport("byteLength")]
    public int ByteLength
    {
        get
        {
            if (isDetached)
                throw JSEngine.NewTypeError("Cannot access byteLength of a detached ArrayBuffer");

            return buffer.Length;
        }
    }

    // ---------------------------------------------------------------
    // §2.9  ArrayBuffer.prototype.detached (getter)
    // ---------------------------------------------------------------

    [JSExport("detached")]
    public bool Detached => isDetached;

    // ---------------------------------------------------------------
    // §2.9.1  ArrayBuffer.prototype.transfer(newLength?)
    // ---------------------------------------------------------------

    [JSExport("transfer")]
    internal JSValue Transfer(in Arguments a)
    {
        if (isDetached)
            throw JSEngine.NewTypeError("Cannot transfer a detached ArrayBuffer");

        int newLength = a.Length > 0
            ? a.Get1().AsInt32OrDefault()
            : buffer.Length;

        if (newLength < 0)
            throw JSEngine.NewRangeError("Invalid ArrayBuffer length");

        var newBuffer = new byte[newLength];
        System.Array.Copy(buffer, newBuffer, Math.Min(buffer.Length, newLength));

        // Detach the source buffer.
        isDetached = true;
        buffer = System.Array.Empty<byte>();

        return new JSArrayBuffer(newBuffer);
    }

    // ---------------------------------------------------------------
    // §2.9.2  ArrayBuffer.prototype.transferToFixedLength(newLength?)
    // ---------------------------------------------------------------

    [JSExport("transferToFixedLength")]
    internal JSValue TransferToFixedLength(in Arguments a)
    {
        // In engines without resizable buffers the behaviour is identical
        // to transfer().  Broiler.JavaScript does not support resizable ArrayBuffers,
        // so the result is always a fixed-length buffer.
        return Transfer(in a);
    }

    // ---------------------------------------------------------------
    // §2.9.3  ArrayBuffer.prototype.slice(begin, end)
    // ---------------------------------------------------------------

    [JSExport("slice")]
    internal JSValue Slice(in Arguments a)
    {
        if (isDetached)
            throw JSEngine.NewTypeError("Cannot slice a detached ArrayBuffer");

        int len = buffer.Length;
        var (beginVal, endVal) = a.Get2();

        int begin = beginVal.IsUndefined ? 0 : beginVal.IntValue;
        int end = endVal.IsUndefined ? len : endVal.IntValue;

        if (begin < 0) begin = Math.Max(len + begin, 0);
        else begin = Math.Min(begin, len);

        if (end < 0) end = Math.Max(len + end, 0);
        else end = Math.Min(end, len);

        int newLen = Math.Max(end - begin, 0);
        var newBuf = new byte[newLen];
        System.Array.Copy(buffer, begin, newBuf, 0, newLen);
        return new JSArrayBuffer(newBuf);
    }
}
