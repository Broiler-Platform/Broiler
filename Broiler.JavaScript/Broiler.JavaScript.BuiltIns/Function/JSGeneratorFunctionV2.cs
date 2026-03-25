using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Function;

public class JSGeneratorFunctionV2 : JSFunction
{
    readonly JSGeneratorDelegateV2 @delegate;

    public JSGeneratorFunctionV2(JSGeneratorDelegateV2 @delegate, in StringSpan name, in StringSpan code) : base(null, name, code)
    {
        this.@delegate = @delegate;
        f = InvokeFunction;
    }

    public override JSValue InvokeFunction(in Arguments a) => JSGeneratorBuilder.CreateFromClrV2(new ClrGeneratorV2(this, @delegate, a));
}
