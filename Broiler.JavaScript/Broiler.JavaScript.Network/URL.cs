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
    [JSClassGenerator("URL")]
    public partial class URL: JSObject
    {
    
        public URL(in Arguments a): base(JSEngine.NewTargetPrototype)
        {
            
        }
    }

    [JSClassGenerator]
    public partial class URLSearchParams: KeyValueStore
    {
        public URLSearchParams(in Arguments a): base(JSEngine.NewTargetPrototype)
        {
            
        }
    }
}
