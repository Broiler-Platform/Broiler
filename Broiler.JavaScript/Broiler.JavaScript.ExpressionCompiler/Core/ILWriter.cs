#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.ExpressionCompiler.Core;


public class ILWriter(ILGenerator il, TextWriter? writer = null)
{
    private readonly LinkedStack<ILTryBlock> tryStack = new();
    private readonly ILGenerator il = il;
    private readonly Sequence<TempVariable> temps = [];

    public TempVariable NewTemp(Type type) => TempVariable.New(this, type);

    public class TempVariable : IDisposable
    {
        private bool IsBusy;

        public readonly LocalBuilder Local;

        public readonly int LocalIndex;

        private TempVariable(ILWriter writer, Type type)
        {
            Local = writer.il.DeclareLocal(type);
            LocalIndex = Local.LocalIndex;
        }

        public static TempVariable New(ILWriter writer, Type type)
        {
            var f = writer.temps.FirstOrDefault(type, (x, t) => x.IsBusy == false && x.Local.LocalType == t);
            if (f != null)
            {
                f.IsBusy = true;
                return f;
            }

            f = new TempVariable(writer, type);
            writer.temps.Add(f);
            f.IsBusy = true;
            return f;
        }

        public void Dispose() => IsBusy = false;
    }

    internal void Emit(in OpCode @switch, Label[] labels) => il.Emit(@switch, labels);

    public int ILOffset => il.ILOffset;

    public bool IsTryBlock => tryStack.Top != null;

    public ILTryBlock Top => tryStack.Top;

    public override string ToString() => writer?.ToString() ?? "";

    private void PrintOffset()
    {
        if (writer == null)
            return;

        writer.Write("IL_");
        writer.Write(il.ILOffset.ToString("X4"));
        writer.Write(": ");
    }

    public void Branch(ILWriterLabel label, int index = -1)
    {
        if (tryStack.Top != null)
        {
            tryStack.Top.Branch(label, index);
            return;
        }

        Goto(label, index);
    }

    internal ILWriterLabel DefineLabel(string label, ILTryBlock? tryBlock = null) => new(il.DefineLabel(), label, tryBlock);

    public void Comment(string comment) => writer?.WriteLine($"// {comment}");

    internal void Goto(ILWriterLabel label, int index = -1)
    {
        if (index >= 0)
        {
            this.EmitLoadLocal(index);
        }

        PrintOffset();
        writer?.WriteLine($"{OpCodes.Br} {label}");
        il.Emit(OpCodes.Br, label.Value);
    }

    internal void Emit(in OpCode code)
    {
        PrintOffset();
        writer?.WriteLine(code.Name);
        il.Emit(code);
    }

    internal void Emit(in OpCode code, short value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value}");
        il.Emit(code, value);
    }

    internal void Verify()
    {
    }

    internal void Emit(in OpCode code, int value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value}");
        il.Emit(code, value);
    }

    internal void Emit(in OpCode code, long value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value}");
        il.Emit(code, value);
    }

    internal void MarkLabel(ILWriterLabel label)
    {
        label.Offset = il.ILOffset;
        writer?.WriteLine();
        writer?.WriteLine($"{label}:");
        il.MarkLabel(label.Value);
    }

    internal void Emit(in OpCode code, ILWriterLabel label)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {label}");
        il.Emit(code, label.Value);
    }

    internal void Emit(in OpCode code, FieldInfo field)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {field.DeclaringType.GetFriendlyName()}.{field.Name}");
        il.Emit(code, field);
    }

    internal void Emit(in OpCode code, Type type)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {type.GetFriendlyName()}");
        il.Emit(code, type);
    }

    internal void Emit(in OpCode code, float value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value}");
        il.Emit(code, value);
    }

    internal void Emit(in OpCode code, double value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value}");
        il.Emit(code, value);
    }

    internal void Emit(in OpCode code, ConstructorInfo value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value.DeclaringType.GetFriendlyName()}");
        il.Emit(code, value);

    }

    internal void Emit(in OpCode code, MethodInfo method)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {method.DeclaringType.GetFriendlyName()}.{method.GetFriendlyName()}");
        il.Emit(code, method);
    }

    internal void Emit(in OpCode code, string value)
    {
        PrintOffset();
        writer?.WriteLine($"{code.Name} {value.Quoted()}");
        il.Emit(code, value);
    }

    internal ILTryBlock BeginTry()
    {
        PrintOffset();
        writer?.WriteLine("try:");
        var label = il.BeginExceptionBlock();
        var ilb = tryStack.Push(new ILTryBlock(this, label));
        return ilb;
    }

    internal void BeginCatchBlock(Type type)
    {
        PrintOffset();
        writer?.WriteLine("catch:");
        il.BeginCatchBlock(type);
    }

    internal void BeginFinallyBlock()
    {
        PrintOffset();
        writer?.WriteLine("finally:");
        il.BeginFinallyBlock();
    }

    internal void EndExceptionBlock()
    {
        PrintOffset();
        writer?.WriteLine("end try:");
        il.EndExceptionBlock();
    }
}
