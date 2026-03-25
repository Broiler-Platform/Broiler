using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.Utils;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitTryStatement(AstTryStatement tryStatement)
    {
        var block = VisitStatement(tryStatement.Block);
        var cb = tryStatement.Catch;

        if (cb != null)
        {
            var id = tryStatement.Identifier;
            var pe = this.scope.Top.CreateException(id.Name.Value);
            using var scope = this.scope.Push(new FastFunctionScope(this.scope.Top));
            var v = scope.CreateVariable(id.Name, newScope: true);
            var catchBlock = YExpression.Block(v.Variable.AsSequence(), YExpression.Assign(v.Variable, JSVariableBuilder.NewFromException(pe.Variable, id.Name.Value)), VisitStatement(cb));
            var cbExp = YExpression.Catch(pe.Variable, catchBlock.ToJSValue());

            if (tryStatement.Finally != null)
                return YExpression.TryCatchFinally(block.ToJSValue(), VisitStatement(tryStatement.Finally).ToJSValue(), cbExp);

            return YExpression.TryCatch(block.ToJSValue(), cbExp);
        }

        var @finally = tryStatement.Finally;
        if (@finally != null)
            return YExpression.TryFinally(block.ToJSValue(), VisitStatement(@finally).ToJSValue());

        return JSUndefinedBuilder.Value;
    }
}
