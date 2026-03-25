using System;
using System.Collections.Generic;
using System.Threading;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;

namespace Broiler.JavaScript.Compiler;


public class SharedParserStringMap<T>
{
    private static ConcurrentNameMap parserStringCache = new();
    private SAUint32Map<uint> indexes;
    private (StringSpan Key, T Value)[] storage;

    private uint length;

    public T this[in StringSpan name]
    {
        get
        {
            if (parserStringCache.TryGetValue(in name, out var id))
            {
                if (indexes.TryGetValue(id.Key, out var index))
                    return storage[index].Value;
            }

            return default;
        }
        set
        {
            var a = parserStringCache.Get(in name);
            if (!indexes.TryGetValue(a.Key, out var id))
            {
                id = length++;
                indexes.Put(a.Key) = id;
            }

            Save(id, in name, in value);
        }
    }

    private void Save(uint index, in StringSpan key, in T value)
    {
        storage = storage ?? (new (StringSpan, T)[8]);
        if (index >= storage.Length)
            Array.Resize(ref storage, (((int)index >> 2) + 1) << 2);

        storage[index] = (key, value);
    }

    public bool TryGetValue(in StringSpan name, out T value)
    {
        if (parserStringCache.TryGetValue(in name, out var id))
        {
            if (indexes.TryGetValue(id.Key, out var index))
            {
                value = storage[index].Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public IFastEnumerator<(StringSpan Key, T Value)> AllValues => new Enumerator(this);

    private class Enumerator(SharedParserStringMap<T> map) : IFastEnumerator<(StringSpan Key, T Value)>
    {
        private int index = -1;

        public bool MoveNext(out (StringSpan Key, T Value) item)
        {
            while (true)
            {
                index++;
                if (index < map.length)
                {
                    item = map.storage[index];
                    return true;
                }
                else break;
            }
            item = (StringSpan.Empty, default);
            return false;
        }

        public bool MoveNext(out (StringSpan Key, T Value) item, out int index)
        {
            while (true)
            {
                this.index++;
                if (this.index < map.length)
                {
                    index = this.index;
                    item = map.storage[index];
                    return true;
                }
                else break;
            }

            item = (StringSpan.Empty, default);
            index = 0;
            return false;
        }
    }
}
public class FastFunctionScope : LinkedStackItem<FastFunctionScope>
{
    public class VariableScope : IDisposable
    {
        public YParameterExpression Variable { get; internal set; }
        public YExpression Expression { get; internal set; }
        public string Name { get; internal set; }
        public bool Create { get; internal set; }
        public YExpression Init { get; private set; }

        /// <summary>
        /// Create Variable first and then assign it, in next step.
        /// 
        /// This is required for recursive function as name/instance of function
        /// is null when it is being created and accessed at the same time
        /// </summary>
        public YExpression PostInit { get; private set; }
        public bool InUse { get; internal set; }
        public bool IsTemp { get; internal set; }
        public void Dispose() => InUse = false;

        public void SetPostInit(YExpression exp)
        {
            if (exp == null)
            {
                PostInit = null;
                return;
            }

            if (Variable.Type == typeof(JSVariable))
            {
                if (exp.Type == typeof(JSVariable))
                {
                    PostInit = YExpression.Assign(Variable, exp);
                    return;
                }
            }

            PostInit = YExpression.Assign(Expression, exp);
        }

        public void SetInit(YExpression exp)
        {
            if (Variable.Type == typeof(JSVariable))
            {
                if (exp != null)
                {
                    if (typeof(JSValue).IsAssignableFrom(exp.Type))
                    {
                        Init = YExpression.Assign(Variable, JSVariableBuilder.New(exp, Name));
                    }
                    else
                    {
                        Init = YExpression.Assign(Variable, exp);
                    }
                }
                else
                {
                    Init = YExpression.Assign(Variable, JSVariableBuilder.New(Name));
                }
            }
            else
            {
                if (exp != null)
                {
                    Init = YExpression.Assign(Variable, exp);
                }
            }
        }
    }

    private SharedParserStringMap<VariableScope> variableScopeList = new();

    // BROILER-PATCH: Register an externally-created variable in this scope.
    // Used for function expression names (ES3 §13) where the variable is
    // declared in the parent scope's block but referenced in the function body.
    internal void AddExternalVariable(in StringSpan name, VariableScope scope) => variableScopeList[name] = scope;

    public AstFunctionExpression Function { get; }

    public YExpression ThisExpression => field ??= GetVariable("this", true).Expression;

    // public Expression NewTarget => Expression.Field(ArgumentsExpression, nameof(Broiler.JavaScript.Core.Arguments.NewTarget));

    public bool HasDisposable => _dispoable != null;

    private YParameterExpression _dispoable;
    public YParameterExpression Disposable => _dispoable ??= YExpression.Parameter(typeof(IJSDisposableStack));

    public YExpression ArgumentsExpression { get; }

    public YParameterExpression Arguments { get; }

    public YParameterExpression Context { get; }

    public YParameterExpression StackItem { get; }

    public bool IsRoot => Function == null;

    public LinkedStack<LoopScope> Loop;

    public YExpression Super { get; set; }

    public IEnumerable<VariableScope> Variables
    {
        get
        {
            var en = variableScopeList.AllValues;
            while (en.MoveNext(out var s))
            {
                if (s.Value.Variable != null)
                    yield return s.Value;
            }
        }
    }

    public IEnumerable<YParameterExpression> VariableParameters
    {
        get
        {
            var en = variableScopeList.AllValues;
            while (en.MoveNext(out var s))
            {
                if (s.Value.Variable != null)
                    yield return s.Value.Variable;
            }
        }
    }

    public IEnumerable<YExpression> InitList
    {
        get
        {
            var en = variableScopeList.AllValues;
            while (en.MoveNext(out var s))
            {
                if (s.Value.Init != null)
                    yield return s.Value.Init;
            }

            en = variableScopeList.AllValues;
            while (en.MoveNext(out var s))
            {
                if (s.Value.PostInit != null)
                    yield return s.Value.PostInit;
            }
        }
    }

    public YLabelTarget ReturnLabel { get; }

    public readonly FastFunctionScope TopScope;

    public YParameterExpression Generator { get; set; }

    public YParameterExpression Awaiter { get; set; }

    private static int scopeID = 0;

    public IFastEnumerable<AstClassProperty> MemberInits { get; set; }

    public readonly FastFunctionScope RootScope;

    public FastFunctionScope(FastPool pool, AstFunctionExpression fx, YExpression previousThis = null, YExpression super = null, bool isAsync = false,
        IFastEnumerable<AstClassProperty> memberInits = null, FastFunctionScope previous = null)
    {
        RootScope = previous ?? this;
        TopScope = this;
        var sID = Interlocked.Increment(ref scopeID);
        MemberInits = memberInits;
        Function = fx;
        Super = super;

        if (fx?.Generator ?? false)
        {
            Generator = YExpression.Parameter(typeof(ClrGeneratorV2), "clrGenerator");
        }
        else
        {
            Generator = null;
        }

        if (fx?.Async ?? true)
            Generator = YExpression.Parameter(typeof(ClrGeneratorV2), "clrGenerator");

        if (isAsync && Generator == null)
            Generator = YExpression.Parameter(typeof(ClrGeneratorV2), "clrGenerator");

        Arguments = (fx?.Generator ?? false) ? YExpression.Parameter(typeof(Arguments), $"a-{sID}") : YExpression.Parameter(typeof(Arguments).MakeByRefType(), $"a-{sID}");
        ArgumentsExpression = Arguments;

        if (previousThis == null)
        {
            // this is needed to fix closure over lambda
            // this can be improved
            var t = CreateVariable("this", ArgumentsBuilder.This(Arguments));
            ThisExpression = t.Expression;
        }

        Context = YExpression.Parameter(typeof(JSContext), $"{nameof(Context)}{sID}");
        StackItem = YExpression.Parameter(typeof(CallStackItem), $"{nameof(StackItem)}{sID}");

        Loop = new LinkedStack<LoopScope>();
        TempVariables = [];
        ReturnLabel = YExpression.Label(typeof(JSValue));
    }

    public FastFunctionScope(FastFunctionScope p)
    {
        Function = p.Function;
        TopScope = p.TopScope;
        RootScope = p.RootScope;
        MemberInits = p.MemberInits;
        ArgumentsExpression = p.ArgumentsExpression;
        Generator = p.Generator;
        Awaiter = p.Awaiter;
        TempVariables = p.TempVariables;
        Super = p.Super;
        Context = p.Context;
        StackItem = p.StackItem;
        Loop = p.Loop;
        ReturnLabel = p.ReturnLabel;
    }

    public YExpression this[string name] => GetVariable(name).Expression;

    public VariableScope CreateException(string name)
    {
        var v = new VariableScope { Variable = YExpression.Parameter(typeof(Exception), name + "Exp") };
        variableScopeList[name + DateTime.UtcNow.Ticks] = v;
        v.Expression = v.Variable;

        return v;
    }

    private readonly Sequence<VariableScope> TempVariables;
    private static int id;

    public VariableScope GetTempVariable(Type type = null)
    {
        type = type ?? typeof(JSValue);
        var fe = TopScope.variableScopeList.AllValues;

        while (fe.MoveNext(out var item))
        {
            var v = item.Value;
            if (v.IsTemp && v.Expression.Type == type && !v.InUse)
            {
                v.InUse = true;
                return v;
            }
        }

        var tp = YExpression.Variable(type, "#Temp" + type.Name + id++);
        var temp = new VariableScope
        {
            Create = true,
            Name = tp.Name,
            IsTemp = true,
            InUse = true,
            Expression = tp,
            Variable = tp
        };

        TopScope.variableScopeList[temp.Name] = temp;
        return temp;
    }

    public bool IsFunctionScope => Parent?.Function != Function;

    public VariableScope CreateVariable(in StringSpan name, YExpression init = null, bool newScope = false, Type type = null)
    {
        var v = variableScopeList[name];
        if (v != null)
            return v;

        // search parent if it is in same function scope...
        if (!newScope)
        {
            var p = Parent;
            while (p != null && p.Function == Function)
            {
                v = p.variableScopeList[name];
                if (v != null)
                    return v;

                p = p.Parent;
            }
        }

        // we need to move variable in top scope...
        var pe = YExpression.Parameter(type ?? typeof(JSVariable), name.Value);
        var ve = JSVariable.ValueExpression(pe);
        
        v = new VariableScope
        {
            Name = name.Value,
            Expression = ve,
            Variable = pe,
            Create = true
        };
        
        v.SetInit(init);
        variableScopeList[name] = v;
        
        return v;
    }

    public VariableScope GetVariable(in StringSpan name, bool createClosure = true)
    {

        var start = this;
        while (start != null)
        {
            if (start.variableScopeList.TryGetValue(name, out var result))
                return result;

            start = start.Parent;
        }

        if (!createClosure)
            throw new ArgumentOutOfRangeException($"{name} not found in current variable scope");

        return null;
    }
}
