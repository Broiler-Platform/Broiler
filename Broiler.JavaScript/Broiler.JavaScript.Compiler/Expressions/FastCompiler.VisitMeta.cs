using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitMeta(AstMeta astMeta)
    {
        // only new.target is supported....
        if (!(astMeta.Identifier.Name.Equals("new") && astMeta.Property.Name.Equals("target")))
            throw JSContext.NewSyntaxError($"{astMeta.Identifier.Name}.{astMeta.Property} not supported");

        return JSContextBuilder.NewTarget();
    }
}
