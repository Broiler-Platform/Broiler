#nullable enable
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator()]
    public partial class AbortController : JSObject
    {
        public AbortController(in Arguments a) : base(JSEngine.NewTargetPrototype)
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
