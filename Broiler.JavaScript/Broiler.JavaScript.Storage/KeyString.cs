using Broiler.JavaScript.Ast.Misc;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Storage;


public enum KeyType
{
    Empty = 0,
    UInt = 1,
    String = 2,
    Symbol = 3
}


[DebuggerDisplay("Key:{Key},{Value}")]
public readonly struct KeyString
{
    public readonly static KeyString Empty = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KeyString(string value) => KeyStrings.GetOrCreate(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator KeyString(in StringSpan value) => KeyStrings.GetOrCreate(value);

    // private readonly KeyType Type;
    // public readonly StringSpan Value;
    public readonly uint Key;

    public bool HasValue => Key != 0;

    internal KeyString(uint key) => Key = key;

    public override bool Equals(object obj)
    {
        if (obj is KeyString k)
            return Key == k.Key;

        return false;
    }

    public override int GetHashCode() => (int)Key;

    public override string ToString() => KeyStrings.GetNameString(Key).Value;

    public StringSpan Value => KeyStrings.GetNameString(Key);
}
