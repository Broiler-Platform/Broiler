using YantraJS.ExpHelper;
using Expression = YantraJS.Expressions.YExpression;
namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitIdentifier(AstIdentifier identifier)
    {
        if (identifier.Name.Equals("undefined"))
            return JSUndefinedBuilder.Value;
        if (identifier.Name.Equals("this"))
            return scope.Top.ThisExpression;
        if (identifier.Name.Equals("arguments"))
        {
            var functionScope = scope.Top.RootScope;
            var vs = functionScope.CreateVariable("arguments",
                JSArgumentsBuilder.New(functionScope.ArgumentsExpression));
            return vs.Expression;

        }
        var var = scope.Top.GetVariable(identifier.Name, true);
        if (var != null)
            return var.Expression;

        return ExpHelper.JSContextBuilder.Index(KeyOfName(identifier.Name));
    }
}
