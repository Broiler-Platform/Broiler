using System;
using System.Collections.Generic;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.Utils;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.Compiler;

public partial class FastCompiler : AstMapVisitor<YExpression>
{
    private readonly FastPool pool;

    readonly LinkedStack<FastFunctionScope> scope = new();
    private readonly string location;

    public LoopScope LoopScope => scope.Top.Loop.Top;

    private StringArray _keyStrings = new();
    private YParameterExpression scriptInfo;

    public YExpression<JSFunctionDelegate> Method { get; }

    public FastCompiler(in StringSpan code, string location = null, IList<string> argsList = null, ICodeCache codeCache = null)
    {
        pool = new FastPool();

        location = location ?? "vm.js";
        this.location = location;

        // add top level...

        var parserPool = new FastPool();
        var parser = new Broiler.JavaScript.Parser.FastParser(new FastTokenStream(parserPool, code));
        var jScript = parser.ParseProgram();
        parserPool.Dispose();

        using var fx = scope.Push(new FastFunctionScope(pool, null, isAsync: jScript.IsAsync));

        var lScope = fx.Context;

        if (argsList != null && jScript.HoistingScope != null)
        {
            var list = new Sequence<StringSpan>(jScript.HoistingScope.Count);
            var e = jScript.HoistingScope.GetFastEnumerator();

            while (e.MoveNext(out var a))
            {
                if (argsList.Contains(a.Value))
                    continue;

                list.Add(a);
            }

            jScript.HoistingScope = list;
        }

        scriptInfo = YExpression.Parameter(typeof(ScriptInfo));

        var args = fx.ArgumentsExpression;
        var te = ArgumentsBuilder.This(args);
        var stackItem = fx.StackItem;
        var vList = new Sequence<YParameterExpression>() { scriptInfo, lScope, stackItem };

        if (argsList != null)
        {
            int i = 0;
            foreach (var arg in argsList)
            {
                // global arguments are set here for FunctionConstructor
                fx.CreateVariable(arg, JSVariableBuilder.FromArgument(fx.ArgumentsExpression, i++, arg));
            }
        }

        var l = fx.ReturnLabel;
        var script = Visit(jScript);
        var sList = new Sequence<YExpression>()
        {
            YExpression.Assign(scriptInfo, ScriptInfoBuilder.New(location,code.Value)),
            YExpression.Assign(lScope, JSContextBuilder.Current)
        };

        JSContextStackBuilder.Push(sList, lScope, stackItem, YExpression.Constant(location), StringSpanBuilder.Empty, 0, 0);
        sList.Add(ScriptInfoBuilder.Build(scriptInfo, _keyStrings));

        vList.AddRange(fx.VariableParameters);
        sList.AddRange(fx.InitList);

        // register globals..
        foreach (var v in fx.Variables)
        {
            if (v.Variable != null && v.Variable.Type == typeof(JSVariable))
            {
                if (argsList?.Contains(v.Name) ?? false)
                    continue;

                if (v.Name == "this")
                    continue;

                sList.Add(JSContextBuilder.Register(lScope, v.Variable));
            }
        }

        sList.Add(YExpression.Return(l, script.ToJSValue()));
        sList.Add(YExpression.Label(l, JSUndefinedBuilder.Value));

        script = YExpression.Block(vList, YExpression.TryFinally(YExpression.Block(sList), JSContextStackBuilder.Pop(stackItem, lScope)));

        if (jScript.IsAsync)
        {
            var g = GeneratorRewriter.Rewrite("vm", script, fx.ReturnLabel, fx.Generator, replaceArgs: fx.Arguments, replaceStackItem: fx.StackItem,
                replaceContext: fx.Context, replaceScriptInfo: scriptInfo);

            var jsf = JSAsyncFunctionBuilder.Create(JSGeneratorFunctionBuilderV2.New(g, StringSpanBuilder.New("vm"), StringSpanBuilder.New(code.Value)));
            var np = YExpression.Parameter(ArgumentsBuilder.refType, "a");

            jsf = JSFunctionBuilder.InvokeFunction(jsf, np);

            Method = YExpression.Lambda<JSFunctionDelegate>("vm", jsf, [np]);
            return;
        }

        var lambda = YExpression.Lambda<JSFunctionDelegate>("body", script, fx.Arguments);
        Method = lambda;
    }

    private YExpression VisitExpression(AstExpression exp) => Visit(exp);

    private YExpression VisitStatement(AstStatement exp) => Visit(exp);

    protected override YExpression VisitClassStatement(AstClassExpression classStatement) => CreateClass(classStatement.Identifier, classStatement.Base, classStatement);

    protected override YExpression VisitContinueStatement(AstContinueStatement continueStatement)
    {
        string name = continueStatement.Label?.Name.Value;
        if (name != null)
        {
            var target = LoopScope.Get(name);
            return target == null ? throw JSEngine.NewSyntaxError($"No label found for {name}") : YExpression.Continue(target.Break);
        }

        return YExpression.Continue(scope.Top.Loop.Top.Continue);
    }

    protected override YExpression VisitDebuggerStatement(AstDebuggerStatement debuggerStatement) => JSDebuggerBuilder.RaiseBreak();

    protected override YExpression VisitEmptyExpression(AstEmptyExpression emptyExpression) => YExpression.Empty;

    protected override YExpression VisitExpressionStatement(AstExpressionStatement expressionStatement) => Visit(expressionStatement.Expression);

    protected override YExpression VisitFunctionExpression(AstFunctionExpression functionExpression) => CreateFunction(functionExpression);

    protected override YExpression VisitSpreadElement(AstSpreadElement spreadElement) => throw new NotImplementedException();

    protected override YExpression VisitThrowStatement(AstThrowStatement throwStatement) => JSExceptionBuilder.Throw(VisitExpression(throwStatement.Argument));

    protected override YExpression VisitYieldExpression(AstYieldExpression yieldExpression)
    {
        var target = VisitExpression(yieldExpression.Argument);
        return YExpression.Yield(target, yieldExpression.Delegate);
    }
}
