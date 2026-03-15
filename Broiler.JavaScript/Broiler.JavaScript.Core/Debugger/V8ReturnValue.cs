using System;

namespace YantraJS.Core.Debugger;

public class V8ReturnValue
{

    public static implicit operator V8ReturnValue(Exception ex) => new(ex);

    public static implicit operator V8ReturnValue(JSValue result) => new()
    { 
        Result = new V8RemoteObject(result)
    };

    public V8ReturnValue()
    {

    }

    public V8ReturnValue(Exception ex, JSContext c = null) => ExceptionDetails = new V8ExceptionDetails(ex, c);

    public V8ExceptionDetails ExceptionDetails { get; set; }

    public string ScriptId { get; set; }
    public object Result { get; set; }
    public string Id { get; set; }
    public string ScriptSource { get; set; }
}
