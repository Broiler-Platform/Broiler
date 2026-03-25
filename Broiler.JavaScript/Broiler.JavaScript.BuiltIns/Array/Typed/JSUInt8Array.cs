using Broiler.JavaScript.ExpressionCompiler;
using System;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Array.Typed;

[JSClassGenerator("Uint8Array"), JSBaseClass("TypedArray")]
public partial class JSUInt8Array : JSTypedArray
{
    [JSExport("BYTES_PER_ELEMENT")]
    internal static readonly int BYTES_PER_ELENENT = 1;

    [JSExport(Length = 3)]
    public JSUInt8Array(in Arguments a) : base(new TypedArrayParameters(a, BYTES_PER_ELENENT)) { }

    private JSUInt8Array(TypedArrayParameters a) : base(a) { }

    internal JSUInt8Array(byte[] data) : base(new TypedArrayParameters(data, BYTES_PER_ELENENT)) { }

    public override JSValue GetValue(uint index, JSValue receiver, bool throwError = true)
    {
        if (index < 0 || index >= length)
            return JSUndefined.Value;
        return new JSNumber(buffer.buffer[byteOffset + index]);
    }

    public override bool SetValue(uint index, JSValue value, JSValue receiver, bool throwError = true)
    {
        if (index < 0 || index >= length)
            return false;
        buffer.buffer[byteOffset + index] = (byte)(uint)value.IntValue;
        return true;
    }

    [JSExport(Length = 1)]
    public static JSValue From(in Arguments a) => new JSUInt8Array(TypedArrayParameters.From(in a, BYTES_PER_ELENENT));

    [JSExport]
    public static JSValue Of(in Arguments a)
    {
        var r = new JSUInt8Array(TypedArrayParameters.Of(in a, BYTES_PER_ELENENT));
        for (int i = 0; i < a.Length; i++)
        {
            r[(uint)i] = a[i];
        }
        return r;
    }

    /// <summary>
    /// ES2026 §4.3.1 — Uint8Array.fromBase64(str)
    /// Creates a new Uint8Array from a Base64-encoded string.
    /// </summary>
    [JSExport("fromBase64")]
    public static JSValue FromBase64(in Arguments a)
    {
        var str = a.Get1();
        if (!str.IsString)
            throw JSEngine.NewTypeError("Uint8Array.fromBase64 requires a string argument");
        var bytes = System.Convert.FromBase64String(str.ToString());
        return new JSUInt8Array(bytes);
    }

    /// <summary>
    /// ES2026 §4.3.3 — Uint8Array.fromHex(str)
    /// Creates a new Uint8Array from a hex-encoded string.
    /// </summary>
    [JSExport("fromHex")]
    public static JSValue FromHex(in Arguments a)
    {
        var str = a.Get1();
        if (!str.IsString)
            throw JSEngine.NewTypeError("Uint8Array.fromHex requires a string argument");
        var hex = str.ToString();
        if (hex.Length % 2 != 0)
            throw JSEngine.NewSyntaxError("Invalid hex string length");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return new JSUInt8Array(bytes);
    }

    /// <summary>
    /// ES2026 §4.3.2 — Uint8Array.prototype.toBase64()
    /// Returns a Base64-encoded string of the typed array content.
    /// </summary>
    [JSExport("toBase64")]
    public JSValue ToBase64(in Arguments a)
    {
        var src = new byte[length];
        System.Array.Copy(buffer.buffer, byteOffset, src, 0, length);
        return new JSString(System.Convert.ToBase64String(src));
    }

    /// <summary>
    /// ES2026 §4.3.4 — Uint8Array.prototype.toHex()
    /// Returns a hex-encoded string of the typed array content.
    /// </summary>
    [JSExport("toHex")]
    public JSValue ToHex(in Arguments a)
    {
        var src = new byte[length];
        System.Array.Copy(buffer.buffer, byteOffset, src, 0, length);
        return new JSString(System.Convert.ToHexString(src).ToLowerInvariant());
    }

    /// <summary>
    /// ES2026 §4.3.5 — Uint8Array.prototype.setFromBase64(str)
    /// Decodes a Base64 string and writes bytes into this typed array.
    /// Returns an object { read, written }.
    /// </summary>
    [JSExport("setFromBase64")]
    public JSValue SetFromBase64(in Arguments a)
    {
        var str = a.Get1();
        if (!str.IsString)
            throw JSEngine.NewTypeError("setFromBase64 requires a string argument");
        var bytes = System.Convert.FromBase64String(str.ToString());
        int written = Math.Min(bytes.Length, length);
        System.Array.Copy(bytes, 0, buffer.buffer, byteOffset, written);
        var result = new JSObject();
        result["read"] = new JSNumber(str.ToString().Length);
        result["written"] = new JSNumber(written);
        return result;
    }

    /// <summary>
    /// ES2026 §4.3.5 — Uint8Array.prototype.setFromHex(str)
    /// Decodes a hex string and writes bytes into this typed array.
    /// Returns an object { read, written }.
    /// </summary>
    [JSExport("setFromHex")]
    public JSValue SetFromHex(in Arguments a)
    {
        var str = a.Get1();
        if (!str.IsString)
            throw JSEngine.NewTypeError("setFromHex requires a string argument");
        var hex = str.ToString();
        if (hex.Length % 2 != 0)
            throw JSEngine.NewSyntaxError("Invalid hex string length");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        int written = Math.Min(bytes.Length, length);
        System.Array.Copy(bytes, 0, buffer.buffer, byteOffset, written);
        var result = new JSObject();
        result["read"] = new JSNumber(written * 2);
        result["written"] = new JSNumber(written);
        return result;
    }
}
