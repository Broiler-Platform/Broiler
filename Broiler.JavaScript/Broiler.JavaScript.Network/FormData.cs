#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator]
    public partial class FormData : KeyValueStore
    {
        public FormData(in Arguments a) : base(JSEngine.NewTargetPrototype)
        {
        }

        internal FormData(JSValue? first) : this()
        {
        }

        [JSExport]
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach(var pair in this.GetEnumerable())
            {
                sb.Append(Uri.EscapeDataString(pair.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(pair.Value));
                sb.Append('&');
            }
            return sb.ToString();
        }
    }
}
