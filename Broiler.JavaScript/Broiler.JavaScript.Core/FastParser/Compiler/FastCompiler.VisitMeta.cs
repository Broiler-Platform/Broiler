using YantraJS.ExpHelper;

using Exp = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitMeta(AstMeta astMeta)
    {
        // only new.target is supported....
        if (!(astMeta.Identifier.Name.Equals("new") 
            &&  astMeta.Property.Name.Equals("target")))
            throw JSContext.Current.NewSyntaxError($"{astMeta.Identifier.Name}.{astMeta.Property} not supported");

        return JSContextBuilder.NewTarget();
    }
}
