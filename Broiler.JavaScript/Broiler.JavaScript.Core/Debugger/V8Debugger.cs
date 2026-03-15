namespace YantraJS.Core.Debugger;

public partial class V8Debugger(V8InspectorProtocol inspectorContext) : V8ProtocolObject(inspectorContext)
{
    public object Enable() => new
    {
        debuggerId = inspectorContext.ID
    };

    public object SetPauseOnExceptions(SetPauseOnExceptionsParams p) => new { };

    public object SetAsyncCallStackDepth(SetAsyncCallStackDepthParams p) => new { };

    public V8ReturnValue GetScriptSource(GetScriptSourceArgs a)
    {
        if(!inspectorContext.Scripts.TryGetValue(a.ScriptId, out var script))
        {
            return new V8ReturnValue { };
        }
        return new V8ReturnValue { 
            ScriptSource = script
        };
    }
}
