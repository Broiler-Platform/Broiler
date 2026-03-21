using System;
using System.Collections.Generic;
using System.Text;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.ExpressionCompiler;

namespace YantraJS.Network
{
    [JSClassGenerator("URL")]
    public partial class URL: JSObject
    {
    
        public URL(in Arguments a): base(JSContext.NewTargetPrototype)
        {
            
        }
    }

    [JSClassGenerator]
    public partial class URLSearchParams: KeyValueStore
    {
        public URLSearchParams(in Arguments a): base(JSContext.NewTargetPrototype)
        {
            
        }
    }
}
