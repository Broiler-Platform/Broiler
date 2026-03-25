using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitMeta(AstMeta astMeta)
    {
        // only new.target is supported....
        if (!(astMeta.Identifier.Name.Equals("new") && astMeta.Property.Name.Equals("target")))
            throw JSEngine.NewSyntaxError($"{astMeta.Identifier.Name}.{astMeta.Property} not supported");

        return JSContextBuilder.NewTarget();
    }
}
