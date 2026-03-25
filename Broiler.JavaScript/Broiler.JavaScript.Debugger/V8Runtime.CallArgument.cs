using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Debugger;


public partial class V8Runtime
{
    public class CallArgument
    {
        public object Value { get; set; }

        public string UnserializableValue { get; set; }

        public string ObjectId { get; set; }

        public JSValue ToJSValue()
        {
            if (ObjectId != null)
                return V8RemoteObject.From(ObjectId);

            return JSEngine.ClrInterop.Marshal(Value);
        }
    }
}
