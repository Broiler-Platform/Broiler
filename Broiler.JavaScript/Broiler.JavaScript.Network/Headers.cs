#nullable enable
using System;
using System.Collections.Generic;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator("Headers")]
    public partial class Headers : KeyValueStore
    {
        public Headers(in Arguments a) : base(JSContext.NewTargetPrototype)
        {
        }

        internal Headers(JSValue? first) : this()
        {
        }
    }
}
