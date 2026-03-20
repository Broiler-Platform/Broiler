namespace Broiler.JavaScript.Core.Core;


public readonly struct PropertyKey
{
    public readonly KeyType Type;
    public readonly uint Index;
    public readonly KeyString KeyString;
    public readonly JSSymbol Symbol;

    public bool IsUInt => Type == KeyType.UInt;

    public bool IsSymbol => Type == KeyType.Symbol;

    private PropertyKey(KeyType type, uint index, in KeyString key, JSSymbol symbol = null)
    {
        Type = type;
        Index = index;
        KeyString = key;
        Symbol = symbol;
    }

    public static implicit operator PropertyKey(int index) => new(KeyType.UInt, (uint)index, KeyString.Empty);
    public static implicit operator PropertyKey(uint index) => new(KeyType.UInt, index, KeyString.Empty);
    public static implicit operator PropertyKey(in KeyString key) => new(KeyType.String, 0, key);
    public static implicit operator PropertyKey(string key) => new(KeyType.String, 0, KeyStrings.GetOrCreate(key));
    public static implicit operator PropertyKey(JSSymbol key) => new(KeyType.Symbol, key.Key, KeyString.Empty, key);
}
