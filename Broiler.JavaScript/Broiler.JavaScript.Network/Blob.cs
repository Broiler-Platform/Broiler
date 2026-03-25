using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.BuiltIns.Promise;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    internal static class JSArrayBufferExtensions
    {
        public static byte[] ToBuffer(this JSValue value)
        {
            if(value is JSString @string)
            {
                return @string.Encode(System.Text.Encoding.UTF8);
            }

            if (value is JSArrayBuffer @buffer)
            {
                return buffer.Buffer;
            }

            if(value is Blob blob)
            {
                return blob.Buffer;
            }

            // DataView is pending...

            throw JSEngine.NewTypeError($"Failed to convert {value} to ArrayBuffer");
        }
    }

    [JSClassGenerator]
    public partial class Blob : JSObject
    {
        public readonly byte[] Buffer;

        public Blob(in Arguments a) : base(JSEngine.NewTargetPrototype)
        {
            var array = a[0] ?? throw JSEngine.NewTypeError("array is required");
            if(a.TryGetAt(1, out var options))
            {
                var p = options[Names.type];
                this.Type = p.IsNullOrUndefined ? new JSString(StringSpan.Empty) : p;
            }

            // save to array... 
            this.Buffer = array.ToBuffer();
        }

        private Blob(byte[] buffer, JSValue type): this()
        {
            Buffer = buffer;
            Type = type;
        }

        [JSExportSameName]
        public readonly static int None = 1;

        [JSExport]
        public JSValue Type { get; }

        [JSExport]
        public JSValue Size => new JSNumber(Buffer.Length);

        [JSExport]
        public JSValue ArrayBuffer => new JSArrayBuffer(Buffer);

        [JSExport]
        public JSValue Slice(in Arguments a)
        {
            return Slice(a.GetIntAt(0, 0), a.GetIntAt(1, this.Buffer.Length), a[2] ?? Type);
        }

        [JSExport]
        public JSValue Text(in Arguments a)
        {
            return new JSPromise(Task.Run<JSValue>(() =>
                new JSString(System.Text.Encoding.UTF8.GetString(Buffer))));
        }

        [JSExport]
        public JSValue Stream(in Arguments a)
        {
            throw JSEngine.NewTypeError("Not supported yet");
        }

        private JSValue Slice(int offset, int length, JSValue type)
        {
            if(offset < 0)
            {
                offset = length + offset;
            }
            var buffer = new byte[length];
            Array.Copy(Buffer, offset, buffer, 0, length);
            return new Blob(buffer, type);
        }
    }
}
