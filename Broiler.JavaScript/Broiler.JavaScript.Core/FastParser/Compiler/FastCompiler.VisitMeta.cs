using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitMeta(AstMeta astMeta)
    {
        // only new.target is supported....
        if (!(astMeta.Identifier.Name.Equals("new") && astMeta.Property.Name.Equals("target")))
            throw JSContext.NewSyntaxError($"{astMeta.Identifier.Name}.{astMeta.Property} not supported");

        return JSContextBuilder.NewTarget();
    }
}
