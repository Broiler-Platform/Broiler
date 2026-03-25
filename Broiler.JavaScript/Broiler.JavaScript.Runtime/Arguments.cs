#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Runtime;


/// <summary>
/// Represents the arguments passed to a JavaScript function call.
/// Stores up to four inline arguments to avoid array allocation;
/// additional arguments overflow into a heap-allocated array.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct Arguments
{
    /// <summary>An empty arguments instance with <c>undefined</c> as <c>this</c>.</summary>
    public static Arguments Empty;
    private const int MinArray = 5;

    /// <summary>Gets the number of arguments (excluding <c>this</c>).</summary>
    public readonly int Length;

    /// <summary>Gets the <c>this</c> value for the call.</summary>
    public readonly JSValue? This;

    private readonly JSValue? Arg0;
    private readonly JSValue? Arg1;
    private readonly JSValue? Arg2;
    private readonly JSValue? Arg3;

    private readonly JSValue[]? Args;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments CopyForCall()
    {
        switch (Length)
        {
            case 0:
                return new Arguments(JSValue.UndefinedValue);
            case 1:
                return new Arguments(Arg0!);
            case 2:
                return new Arguments(Arg0!, Arg1!);
            case 3:
                return new Arguments(Arg0!, Arg1!, Arg2!);
            case 4:
                return new Arguments(Arg0!, Arg1!, Arg2!, Arg3!);
            case 5:
                return new Arguments(Args![0], Args[1]!, Args[2]!, Args[3]!, Args[4]!);
            default:
                var sa = new JSValue[Length - 1];
                System.Array.Copy(Args, 1, sa, 0, sa.Length);
                return new Arguments(Args![0], sa);
        }
    }

    public Arguments CopyForBind(in Arguments a)
    {
        // need to append a's parameter to self...
        var @this = this[0]!;
        var total = Length - 1 + a.Length;
        var list = new JSValue[total + a.Length];
        int i;

        for (i = 0; i < Length - 1; i++)
            list[i] = this[i + 1]!;

        var start = i;
        for (; i < total; i++)
            list[i] = a[i - start]!;

        return new Arguments(@this, list);
    }

    internal static Func<JSValue, JSValue, Arguments> ForApplyImpl;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Arguments ForApply(JSValue @this, JSValue args) => ForApplyImpl(@this, args);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments CopyForApply()
    {
        // in apply first parameter is @this and rest is An Array
        var (@this, args) = Get2();
        return ForApply(@this, args);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this)
    {
        // NewTarget = null;
        This = @this;
        Length = 0;
        Arg0 = null;
        Arg1 = null;
        Arg2 = null;
        Arg3 = null;
        Args = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this, JSValue a0)
    {
        // NewTarget = null;
        This = @this;
        Length = 1;
        Arg0 = a0;
        Arg1 = null;
        Arg2 = null;
        Arg3 = null;
        Args = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this, JSValue a0, JSValue a1)
    {
        // NewTarget = null;
        This = @this;
        Length = 2;
        Arg0 = a0;
        Arg1 = a1;
        Arg2 = null;
        Arg3 = null;
        Args = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this, JSValue a0, JSValue a1, JSValue a2)
    {
        // NewTarget = null;
        This = @this;
        Length = 3;
        Arg0 = a0;
        Arg1 = a1;
        Arg2 = a2;
        Arg3 = null;
        Args = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this, JSValue a0, JSValue a1, JSValue a2, JSValue a3)
    {
        // NewTarget = null;
        This = @this;
        Length = 4;
        Arg0 = a0;
        Arg1 = a1;
        Arg2 = a2;
        Arg3 = a3;
        Args = null;
    }

    public static Arguments Spread(JSValue @this, params JSValue[] list)
    {
        int length = 0;
        foreach (var item in list)
            length += item.IsSpread ? item.Length : 1;

        return new Arguments(@this, list, length);
    }

    internal static Func<JSValue, JSValue> GetSpreadTarget;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this, JSValue[] list, int length)
    {
        // NewTarget = null;
        This = @this;
        Length = length;
        JSValue[] args = new JSValue[length];
        int i = 0;

        foreach (var a in list)
        {
            if (a.IsSpread)
            {
                var spreadTarget = GetSpreadTarget(a);
                for (uint j = 0; j < (uint)spreadTarget.Length; j++)
                    args[i++] = spreadTarget[j];
                continue;
            }

            args[i++] = a;
        }

        switch (Length)
        {
            case 0:
                Arg0 = null;
                Arg1 = null;
                Arg2 = null;
                Arg3 = null;
                Args = null;
                break;
            case 1:
                Arg0 = args[0];
                Arg1 = null;
                Arg2 = null;
                Arg3 = null;
                Args = null;
                break;
            case 2:
                Arg0 = args[0];
                Arg1 = args[1];
                Arg2 = null;
                Arg3 = null;
                Args = null;
                break;
            case 3:
                Arg0 = args[0];
                Arg1 = args[1];
                Arg2 = args[2];
                Arg3 = null;
                Args = null;
                break;
            case 4:
                Arg0 = args[0];
                Arg1 = args[1];
                Arg2 = args[2];
                Arg3 = args[3];
                Args = null;
                break;
            default:
                Arg0 = null;
                Arg1 = null;
                Arg2 = null;
                Arg3 = null;
                Args = args;
                break;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments(JSValue @this, JSValue[] args)
    {
        This = @this;
        Length = args.Length;

        switch (Length)
        {
            case 0:
                Arg0 = null;
                Arg1 = null;
                Arg2 = null;
                Arg3 = null;
                Args = null;
                break;
            case 1:
                Arg0 = args[0];
                Arg1 = null;
                Arg2 = null;
                Arg3 = null;
                Args = null;
                break;
            case 2:
                Arg0 = args[0];
                Arg1 = args[1];
                Arg2 = null;
                Arg3 = null;
                Args = null;
                break;
            case 3:
                Arg0 = args[0];
                Arg1 = args[1];
                Arg2 = args[2];
                Arg3 = null;
                Args = null;
                break;
            case 4:
                Arg0 = args[0];
                Arg1 = args[1];
                Arg2 = args[2];
                Arg3 = args[3];
                Args = null;
                break;
            default:
                Arg0 = null;
                Arg1 = null;
                Arg2 = null;
                Arg3 = null;
                Args = args;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Arguments(JSValue @this, Arguments src)
    {
        Length = src.Length;
        Arg0 = src.Arg0;
        Arg1 = src.Arg1;
        Arg2 = src.Arg2;
        Arg3 = src.Arg3;
        Args = src.Args;
        This = @this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Arguments(JSValue @this, int length, JSValue? arg0, JSValue? arg1, JSValue? arg2, JSValue? arg3, JSValue[]? args)
    {
        Length = length;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Arg3 = arg3;
        Args = args;
        This = @this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Arguments OverrideThis(JSValue @this) => new(@this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSValue Get1()
    {
        if (Length == 0)
            return JSValue.UndefinedValue;

        if (Length < MinArray)
            return Arg0!;

        return Args![0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (JSValue, JSValue) Get2()
    {
        if (Length == 0)
            return (JSValue.UndefinedValue, JSValue.UndefinedValue);

        if (Length == 1)
            return (Arg0!, JSValue.UndefinedValue);

        if (Length < MinArray)
            return (Arg0!, Arg1!);

        return (Args![0], Args[1]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (JSValue, JSValue) Get2(JSValue def1, JSValue def2)
    {
        if (Length == 0)
            return (def1, def2);

        if (Length == 1)
            return (Arg0!, def2);

        if (Length < MinArray)
            return (Arg0!, Arg1!);

        return (Args![0], Args[1]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    public (JSValue, JSValue, JSValue) Get3()
    {
        if (Length == 0)
            return (JSValue.UndefinedValue, JSValue.UndefinedValue, JSValue.UndefinedValue);

        if (Length == 1)
            return (Arg0!, JSValue.UndefinedValue, JSValue.UndefinedValue);

        if (Length == 2)
            return (Arg0!, Arg1!, JSValue.UndefinedValue);

        if (Length < MinArray)
            return (Arg0!, Arg1!, Arg2!);

        return (Args![0], Args[1], Args[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (JSValue, JSValue, JSValue, JSValue) Get4()
    {
        if (Length == 0)
            return (JSValue.UndefinedValue, JSValue.UndefinedValue, JSValue.UndefinedValue, JSValue.UndefinedValue);

        if (Length == 1)
            return (Arg0!, JSValue.UndefinedValue, JSValue.UndefinedValue, JSValue.UndefinedValue);

        if (Length == 2)
            return (Arg0!, Arg1!, JSValue.UndefinedValue, JSValue.UndefinedValue);

        if (Length == 3)
            return (Arg0!, Arg1!, Arg2!, JSValue.UndefinedValue);

        if (Length < MinArray)
            return (Arg0!, Arg1!, Arg2!, Arg3!);

        return (Args![0], Args[1], Args[2], Args[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int, int, int, int, int, int, int) Get7Int()
    {
        if (Length == 0)
            return (0, 0, 1, 0, 0, 0, 0);

        if (Length == 1)
            return (Arg0!.IntValue, 0, 1, 0, 0, 0, 0);

        if (Length == 2)
            return (Arg0!.IntValue, Arg1!.IntValue, 1, 0, 0, 0, 0);

        if (Length == 3)
            return (Arg0!.IntValue, Arg1!.IntValue, Arg2!.IntValue, 0, 0, 0, 0);

        if (Length == 4)
            return (Arg0!.IntValue, Arg1!.IntValue, Arg2!.IntValue, Arg3!.IntValue, 0, 0, 0);

        if (Length == 5)
            return (Args![0].IntValue, Args[1].IntValue, Args[2].IntValue, Args[3].IntValue, Args[4].IntValue, 0, 0);

        if (Length == 6)
            return (Args![0].IntValue, Args[1].IntValue, Args[2].IntValue, Args[3].IntValue, Args[4].IntValue, Args[5].IntValue, 0);

        return (Args![0].IntValue, Args[1].IntValue, Args[2].IntValue, Args[3].IntValue, Args[4].IntValue, Args[5].IntValue, Args[6].IntValue);
    }

    public JSValue[]? GetArgs() => Args;

    static readonly JSValue[] _Empty = [];

    public JSValue[] ToArray()
    {
        return Length switch
        {
            0 => _Empty,
            1 => [Arg0!],
            2 => [Arg0!, Arg1!],
            3 => [Arg0!, Arg1!, Arg2!],
            4 => [Arg0!, Arg1!, Arg2!, Arg3!],
            _ => Args!,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAt(int index, out JSValue a)
    {
        if (Length > index)
        {
            if (Length >= MinArray)
            {
                a = Args![index];
                return true;
            }

            a = index switch
            {
                0 => Arg0!,
                1 => Arg1!,
                2 => Arg2!,
                3 => Arg3!,
                _ => Args![index],
            };

            return true;
        }

        a = null!;
        return false;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIntAt(int index, int def)
    {
        if (Length > index)
        {
            if (Length >= MinArray)
                return Args![index].IntValue;

            return index switch
            {
                0 => Arg0!.IntValue,
                1 => Arg1!.IntValue,
                2 => Arg2!.IntValue,
                3 => Arg3!.IntValue,
                _ => Args![index].IntValue,
            };
        }

        return def;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIntegerAt(int index, int def)
    {
        if (Length > index)
        {
            if (Length >= MinArray)
                return Args![index].IntegerValue;

            return index switch
            {
                0 => Arg0!.IntegerValue,
                1 => Arg1!.IntegerValue,
                2 => Arg2!.IntegerValue,
                3 => Arg3!.IntegerValue,
                _ => Args![index].IntegerValue,
            };
        }

        return def;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDoubleAt(int index, double def)
    {
        if (Length > index)
        {
            return index switch
            {
                0 => Arg0!.DoubleValue,
                1 => Arg1!.DoubleValue,
                2 => Arg2!.DoubleValue,
                3 => Arg3!.DoubleValue,
                _ => Args![index].DoubleValue,
            };
        }

        return def;
    }



    public JSValue? this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Length > index)
            {
                if (Length >= MinArray)
                    return Args![index];

                return index switch
                {
                    0 => Arg0,
                    1 => Arg1,
                    2 => Arg2,
                    3 => Arg3,
                    _ => Args![index],
                };
            }

            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JSValue GetAt(int index)
    {
        if (Length >= MinArray)
            return index < Length ? Args![index] : JSValue.UndefinedValue;

        return index switch
        {
            0 => Arg0 ?? JSValue.UndefinedValue,
            1 => Arg1 ?? JSValue.UndefinedValue,
            2 => Arg2 ?? JSValue.UndefinedValue,
            3 => Arg3 ?? JSValue.UndefinedValue,
            _ => index >= Length ? JSValue.UndefinedValue : Args![index],
        };
    }

    internal static Func<Arguments, uint, JSValue> RestFromImpl;

    public JSValue RestFrom(uint index) => RestFromImpl(this, index);


    [EditorBrowsable(EditorBrowsableState.Never)]
    public IElementEnumerator GetElementEnumerator() => new ArgumentsElementEnumerator(this);

    internal static Func<JSValue, string, string, string, int, StringSpan> GetStringImpl;

    public StringSpan GetString(int index, string name, [CallerMemberName] string? function = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int line = 0) =>
        GetStringImpl(this[index], name, function, filePath, line);

    struct ArgumentsElementEnumerator(Arguments arguments) : IElementEnumerator
    {
        private int index = -1;

        public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
        {
            if ((++this.index) > arguments.Length)
            {
                index = (uint)this.index;
                value = arguments.GetAt(this.index);
                hasValue = true;
                return true;
            }

            index = 0;
            value = JSValue.UndefinedValue;
            hasValue = false;
            return false;
        }

        public bool MoveNext(out JSValue value)
        {
            if ((++index) > arguments.Length)
            {
                value = arguments.GetAt(index);
                return true;
            }

            value = JSValue.UndefinedValue;
            return false;
        }

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            if ((++index) > arguments.Length)
            {
                value = arguments.GetAt(index);
                return true;
            }

            value = @default;
            return false;
        }

        public JSValue NextOrDefault(JSValue @default)
        {
            if ((++index) > arguments.Length)
                return arguments.GetAt(index);

            return @default;
        }
    }
}
