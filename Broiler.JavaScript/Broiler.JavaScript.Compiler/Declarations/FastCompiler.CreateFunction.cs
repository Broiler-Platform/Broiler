using Broiler.JavaScript.ExpressionCompiler.Expressions;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    private YExpression CreateFunction(AstFunctionExpression functionDeclaration, YExpression super = null, bool createClass = false, string className = null,
        IFastEnumerable<AstClassProperty> memberInits = null)
    {
        var node = functionDeclaration;

        // get text...

        var previousScope = scope.Top;

        // if this is an arrowFunction then override previous thisExperssion

        var previousThis = scope.Top.ThisExpression;
        if (!functionDeclaration.IsArrowFunction)
            previousThis = null;

        var functionName = functionDeclaration.Id?.Name.Value;

        // var parentScriptInfo = this.scope.Top.ScriptInfo;

        var nodeCode = node.Code;

        var code = StringSpanBuilder.New(ScriptInfoBuilder.Code(scriptInfo), nodeCode.Offset, nodeCode.Length);
        var sList = new Sequence<YExpression>();
        var bodyInits = new Sequence<YExpression>();
        var vList = new Sequence<YParameterExpression>();

        var current = scope.Top.RootScope;
        var cs = scope.Push(new FastFunctionScope(pool, functionDeclaration, previousThis, super, memberInits: memberInits, previous: functionDeclaration.IsArrowFunction ? current : null));
        {
            var lexicalScopeVar = cs.Context;

            vList.Add(cs.Context);
            vList.Add(cs.StackItem);
            sList.Add(YExpression.Assign(cs.Context, JSContextBuilder.Current));

            FastFunctionScope.VariableScope jsFVarScope = null;

            // BROILER-PATCH: For function declarations, look up name in parent scope
            // to bind the function. For function expressions, the name is local to
            // the function body and must not leak to the parent scope (ES3 §13).
            YParameterExpression fexprNameParam = null;
            if (functionName != null && functionDeclaration.IsStatement)
            {
                jsFVarScope = previousScope.GetVariable(functionName);
            }
            else if (functionName != null && !functionDeclaration.IsStatement)
            {
                // BROILER-PATCH: For function expressions, create a closure variable
                // in the parent scope that the function body captures. This variable
                // holds the function reference and is marked read-only.
                fexprNameParam = YExpression.Parameter(typeof(JSVariable), functionName);
                var fexprVarScope = new FastFunctionScope.VariableScope
                {
                    Name = functionName,
                    Expression = JSVariable.ValueExpression(fexprNameParam),
                    Create = false
                };

                cs.AddExternalVariable(functionName, fexprVarScope);
            }

            var s = cs;
            // use this to create variables...
            // var t = s.ThisExpression;
            var args = s.ArgumentsExpression;
            var stackItem = cs.StackItem;
            var r = s.ReturnLabel;

            YExpression fxName;
            YExpression localFxName;
            int nameOffset;
            int nameLength;

            if (functionName != null)
            {
                var id = functionDeclaration.Id;

                fxName = StringSpanBuilder.New(ScriptInfoBuilder.Code(scriptInfo), id.Name.Offset, id.Name.Length);
                localFxName = StringSpanBuilder.New(ScriptInfoBuilder.Code(scriptInfo), id.Name.Offset, id.Name.Length);

                nameOffset = id.Name.Offset;
                nameLength = id.Name.Length;
            }
            else
            {
                fxName = StringSpanBuilder.Empty;
                localFxName = StringSpanBuilder.Empty;

                nameOffset = 0;
                nameLength = 0;
            }

            var point = node.Start.Start;

            sList.Add(YExpression.Assign(stackItem, CallStackItemBuilder.New(cs.Context, scriptInfo, nameOffset, nameLength, point.Line, point.Column)));

            var argumentElements = args;

            var pe = functionDeclaration.Params.GetFastEnumerator();
            while (pe.MoveNext(out var v, out var i))
            {
                if (v.Identifier.IsSpreadElement(out var spe))
                {
                    CreateAssignment(bodyInits, spe.Argument, ArgumentsBuilder.RestFrom(argumentElements, (uint)i), true, true);
                    continue;
                }

                CreateAssignment(bodyInits, v.Identifier, JSVariableBuilder.FromArgumentOptional(argumentElements, i, VisitExpression(v.Init)), true, true);
            }

            YExpression lambdaBody = VisitStatement(functionDeclaration.Body);

            vList.AddRange(s.VariableParameters);
            sList.AddRange(s.InitList);
            sList.AddRange(bodyInits);

            if (s.MemberInits != null)
                InitMembers(sList, s);

            sList.Add(lambdaBody);

            if (createClass)
                sList.Add(YExpression.Return(r, YExpression.Coalesce(s.ThisExpression, JSExceptionBuilder.Throw("this cannot be null"))));

            sList.Add(YExpression.Label(r, JSUndefinedBuilder.Value));

            var block = YExpression.Block(vList, sList);

            // adding lexical scope pending...

            functionName = functionName ?? "inline";

            static YExpression ToDelegate(YLambdaExpression e1) => e1;

            var scriptFunctionName = new FunctionName(functionName, location, point.Line, point.Column);

            YLambdaExpression lambda;
            YExpression jsf;

            if (functionDeclaration.Generator)
            {
                lambda = GeneratorRewriter.Rewrite(in scriptFunctionName, block, cs.ReturnLabel, cs.Generator, replaceArgs: cs.Arguments, replaceStackItem: cs.StackItem,
                    replaceContext: cs.Context, replaceScriptInfo: scriptInfo);

                jsf = JSGeneratorFunctionBuilderV2.New(lambda, fxName, code);
            }
            else if (functionDeclaration.Async)
            {

                lambda = GeneratorRewriter.Rewrite(in scriptFunctionName, block, cs.ReturnLabel, cs.Generator, replaceArgs: cs.Arguments, replaceStackItem: cs.StackItem,
                    replaceContext: cs.Context, replaceScriptInfo: scriptInfo);

                jsf = JSAsyncFunctionBuilder.Create(JSGeneratorFunctionBuilderV2.New(lambda, fxName, code));
            }
            else
            {
                lambda = YExpression.Lambda(typeof(JSFunctionDelegate), block, in scriptFunctionName, [cs.Arguments]);
                jsf = JSFunctionBuilder.New(ToDelegate(lambda), fxName, code, functionDeclaration.Params.Count);
            }

            cs.Dispose();

            if (jsFVarScope != null)
            {
                jsFVarScope.SetPostInit(jsf);
                return jsFVarScope.Expression;
            }

            // BROILER-PATCH: For function expressions with a name, wrap the result
            // in a block that creates a read-only closure variable holding the
            // function reference. The function body captures this variable.
            if (fexprNameParam != null)
            {
                var isReadOnlyField = typeof(JSVariable).GetField("IsReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                var fexprVars = new Sequence<YParameterExpression> { fexprNameParam };

                return YExpression.Block(fexprVars,
                    YExpression.Assign(fexprNameParam, JSVariableBuilder.New(jsf, functionName)),
                    YExpression.Assign(YExpression.Field(fexprNameParam, isReadOnlyField), YExpression.Constant(true)), 
                    JSVariable.ValueExpression(fexprNameParam));
            }

            return jsf;
        }
    }

    private void InitMembers(Sequence<YExpression> sList, FastFunctionScope s)
    {
        var @this = s.ThisExpression;
        var en = s.MemberInits.GetFastEnumerator();

        while (en.MoveNext(out var member))
        {
            var name = GetName(member);
            var value = member.Init == null ? JSUndefinedBuilder.Value : Visit(member.Init);
            var init = JSObjectBuilder.AddValue(name, value, JSPropertyAttributes.ConfigurableValue);

            sList.Add(YExpression.Call(@this, init.Member as MethodInfo, init.Arguments));
        }
    }
}
