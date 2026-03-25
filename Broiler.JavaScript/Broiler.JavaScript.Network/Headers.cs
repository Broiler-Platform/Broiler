#nullable enable
using System;
using System.Collections.Generic;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator("Headers")]
    public partial class Headers : KeyValueStore
    {
        public Headers(in Arguments a) : base(JSEngine.NewTargetPrototype)
        {
        }

        internal Headers(JSValue? first) : this()
        {
        }
    }
}
