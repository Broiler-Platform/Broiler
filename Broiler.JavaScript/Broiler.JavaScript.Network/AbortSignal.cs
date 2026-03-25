#nullable enable
using System;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.BuiltIns.Events;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator]
    public partial class AbortSignal: EventTarget
    {
        internal AbortSignal(in Arguments a): this(JSEngine.NewTargetPrototype) {
        }


        [JSExport]
        public bool Aborted { get; internal set; }

        [JSExport]
        public string? Reason { get; private set; }

        public event EventHandler? AbortedEvent;

        internal void Abort(string? reason)
        {
            this.Reason = reason ?? "Aborted";
            Aborted = true;
            AbortedEvent?.Invoke(this, EventArgs.Empty);
            var e = Event.Create("abort");
            this.DispatchEvent(e);
        }
    }
}
