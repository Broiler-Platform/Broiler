using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Function;

public class JSClassFunction(JSFunctionDelegate @delegate, in StringSpan name, in StringSpan source, int length = 0) : JSFunction(@delegate, name, source, length)
{
    public override JSValue InvokeFunction(in Arguments a) => throw JSEngine.NewTypeError($"{name} cannot be invoked directly");
}
