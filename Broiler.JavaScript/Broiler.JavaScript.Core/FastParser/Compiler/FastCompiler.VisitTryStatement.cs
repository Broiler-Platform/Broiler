
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.Core.Utils;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitTryStatement(AstTryStatement tryStatement)
    {
        var block = VisitStatement(tryStatement.Block);
        var cb = tryStatement.Catch;

        if (cb != null)
        {
            var id = tryStatement.Identifier;
            var pe = this.scope.Top.CreateException(id.Name.Value);
            using var scope = this.scope.Push(new FastFunctionScope(this.scope.Top));
            var v = scope.CreateVariable(id.Name, newScope: true);
            var catchBlock = Exp.Block(v.Variable.AsSequence(), Exp.Assign(v.Variable, JSVariableBuilder.NewFromException(pe.Variable, id.Name.Value)), VisitStatement(cb));
            var cbExp = Exp.Catch(pe.Variable, catchBlock.ToJSValue());

            if (tryStatement.Finally != null)
                return Exp.TryCatchFinally(block.ToJSValue(), VisitStatement(tryStatement.Finally).ToJSValue(), cbExp);

            return Exp.TryCatch(block.ToJSValue(), cbExp);
        }

        var @finally = tryStatement.Finally;
        if (@finally != null)
            return Exp.TryFinally(block.ToJSValue(), VisitStatement(@finally).ToJSValue());

        return JSUndefinedBuilder.Value;
    }
}
