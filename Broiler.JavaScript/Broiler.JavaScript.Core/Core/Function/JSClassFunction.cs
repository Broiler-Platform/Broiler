using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Core.Core.Function;

public class JSClassFunction(JSFunctionDelegate @delegate, in StringSpan name, in StringSpan source, int length = 0) : JSFunction(@delegate, name, source, length)
{
    public override JSValue InvokeFunction(in Arguments a) => throw JSContext.NewTypeError($"{name} cannot be invoked directly");
}
