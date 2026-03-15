using System;
using System.Collections.Generic;

namespace YantraJS.Core.Debugger;


public partial class V8Runtime
{
    public class ConsoleApiCalled(string id, JSContext context, string type, in Arguments a) : V8ProtocolEvent
    {
        public string Type { get; set; }

        public List<V8RemoteObject> Args { get; set; } = V8RemoteObject.From(in a);
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string ExecutionContextId { get; set; } = id;

        public V8StackTrace StackTrace { get; set; } = new V8StackTrace(context);

        internal override string EventName => "Runtime.consoleAPICalled";
    }
}
