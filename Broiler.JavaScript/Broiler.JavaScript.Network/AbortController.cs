#nullable enable
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator()]
    public partial class AbortController : JSObject
    {
        public AbortController(in Arguments a) : base(JSContext.NewTargetPrototype)
        {
            Signal = new AbortSignal();
        }

        public AbortSignal Signal { get; }

        [JSExport]
        public void Abort(string? name)
        {
            Signal.Abort(name);
        }
    }
}
