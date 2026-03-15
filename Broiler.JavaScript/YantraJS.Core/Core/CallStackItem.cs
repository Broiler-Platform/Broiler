using System.Runtime.CompilerServices;
using YantraJS.Core.CodeGen;
using System.ComponentModel;
using YantraJS.Core.Core;

namespace YantraJS.Core;

public class CallStackItem
{
    private static StringSpan Inline = "inline";

    internal CallStackItem(string fileName, in StringSpan function, int line, int column)
    {
        FileName = fileName;
        Function = function;
        Line = line;
        Column = column;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public CallStackItem(
        JSContext context, 
        ScriptInfo scriptInfo, 
        int nameOffset,
        int nameLength,
        int line,
        int column)
    {
        context = context ?? JSContext.Current;
        context.EnsureSufficientExecutionStack();
        this.context = context;
        var ctx = context.CurrentNewTarget;
        if (ctx != null)
        {
            NewTarget = ctx;
            context.CurrentNewTarget = null;
        }
        FileName = scriptInfo.FileName;
        Function = (nameLength>0) 
            ? scriptInfo.Code.ToStringSpan(nameOffset, nameLength)
            : Inline;
        Line = line;
        Column = column;
        Parent = context.Top;
        context.Top = this;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public CallStackItem(JSContext context, string fileName, in StringSpan function, int line, int column)
    {
        context = context ?? JSContext.Current;
        context.EnsureSufficientExecutionStack();
        this.context = context;
        FileName = fileName;
        Function = function;
        Line = line;
        Column = column;
        Parent = context.Top;
        context.Top = this;
    }

    public CallStackItem Parent;
    public JSFunction NewTarget;
    public StringSpan Function;
    public int Line;
    public int Column;
    private readonly JSContext context;
    public string FileName;

    public void Update() => System.Diagnostics.Debug.WriteLine($"{Function} at {Line}, {Column}");

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Step(int line, int column)
    {
        context.Top = this;
        Line = line;
        Column = column;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Pop(JSContext context)
    {
        context = context ?? JSContext.Current;
        context.Top = Parent;
        Parent = null;
    }

    public override string ToString() => $"{Function} at {FileName} - {Line},{Column}";
}
