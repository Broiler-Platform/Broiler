#nullable enable
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Enumerators;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using YantraJS.Core;

namespace Broiler.JavaScript.Core.Core;


/// <summary>
/// Represents the arguments passed to a JavaScript function call.
/// Stores up to four inline arguments to avoid array allocation;
/// additional arguments overflow into a heap-allocated array.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct Arguments
{
    /// <summary>An empty arguments instance with <c>undefined</c> as <c>this</c>.</summary>
    public static Arguments Empty = new(JSUndefined.Value);
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
                return new Arguments(JSUndefined.Value);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Arguments ForApply(JSValue @this, JSValue args)
    {
        if (args.IsArray && args is JSArray argArray)
        {
            var length = argArray._length;
            switch (length)
            {
                case 0:
                    return new Arguments(@this, 0, null, null, null, null, null);
                case 1:
                    return new Arguments(@this, 1, argArray[0], null, null, null, null);
                case 2:
                    return new Arguments(@this, 2, argArray[0], argArray[1], null, null, null);
                case 3:
                    return new Arguments(@this, 3, argArray[0], argArray[1], argArray[2], null, null);
                case 4:
                    return new Arguments(@this, 4, argArray[0], argArray[1], argArray[2], argArray[3], null);
                default:
                    var argList = new JSValue[argArray._length];
                    var ee = argArray.GetElementEnumerator();
                    while (ee.MoveNext(out var hasValue, out var value, out var index))
                        argList[index] = hasValue ? value : JSUndefined.Value;

                    return new Arguments(@this, (int)length, null, null, null, null, argList);
            }
        }

        if (args is JSArguments arguments)
        {
            var length = arguments.Length;
            switch (length)
            {
                case 0:
                    return new Arguments(@this, 0, null, null, null, null, null);
                case 1:
                    return new Arguments(@this, 1, arguments[0], null, null, null, null);
                case 2:
                    return new Arguments(@this, 2, arguments[0], arguments[1], null, null, null);
                case 3:
                    return new Arguments(@this, 3, arguments[0], arguments[1], arguments[2], null, null);
                case 4:
                    return new Arguments(@this, 4, arguments[0], arguments[1], arguments[2], arguments[3], null);
                default:
                    var argList = new JSValue[arguments.Length];
                    var ee = arguments.GetElementEnumerator();
                    while (ee.MoveNext(out var hasValue, out var value, out var index))
                        argList[index] = hasValue ? value : JSUndefined.Value;

                    return new Arguments(@this, length, null, null, null, null, argList);
            }
        }

        return new Arguments(@this, 0, null, null, null, null, null);
    }


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
            if (a is JSSpreadValue spv)
            {
                for (uint j = 0; j < spv.Length; j++)
                    args[i++] = spv.Value[j];

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
            return JSUndefined.Value;

        if (Length < MinArray)
            return Arg0!;

        return Args![0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (JSValue, JSValue) Get2()
    {
        if (Length == 0)
            return (JSUndefined.Value, JSUndefined.Value);

        if (Length == 1)
            return (Arg0!, JSUndefined.Value);

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
            return (JSUndefined.Value, JSUndefined.Value, JSUndefined.Value);

        if (Length == 1)
            return (Arg0!, JSUndefined.Value, JSUndefined.Value);

        if (Length == 2)
            return (Arg0!, Arg1!, JSUndefined.Value);

        if (Length < MinArray)
            return (Arg0!, Arg1!, Arg2!);

        return (Args![0], Args[1], Args[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (JSValue, JSValue, JSValue, JSValue) Get4()
    {
        if (Length == 0)
            return (JSUndefined.Value, JSUndefined.Value, JSUndefined.Value, JSUndefined.Value);

        if (Length == 1)
            return (Arg0!, JSUndefined.Value, JSUndefined.Value, JSUndefined.Value);

        if (Length == 2)
            return (Arg0!, Arg1!, JSUndefined.Value, JSUndefined.Value);

        if (Length == 3)
            return (Arg0!, Arg1!, Arg2!, JSUndefined.Value);

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
            return index < Length ? Args![index] : JSUndefined.Value;

        return index switch
        {
            0 => Arg0 ?? JSUndefined.Value,
            1 => Arg1 ?? JSUndefined.Value,
            2 => Arg2 ?? JSUndefined.Value,
            3 => Arg3 ?? JSUndefined.Value,
            _ => index >= Length ? JSUndefined.Value : Args![index],
        };
    }

    public JSValue RestFrom(uint index)
    {
        var a = new JSArray();
        ref var ae = ref a.GetElements(true);
        uint i;
        uint ai;

        for (ai = 0, i = index; i < Length; i++, ai++)
            ae.Put(ai, GetAt((int)i));

        a._length = ai;
        return a;
    }


    [EditorBrowsable(EditorBrowsableState.Never)]
    public IElementEnumerator GetElementEnumerator() => new ArgumentsElementEnumerator(this);

    public StringSpan GetString(int index, string name, [CallerMemberName] string? function = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int line = 0) =>
        this[index] is JSString s ? s.value : throw new JSException(name + " is required", function, filePath, line);

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
            value = JSUndefined.Value;
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

            value = JSUndefined.Value;
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
