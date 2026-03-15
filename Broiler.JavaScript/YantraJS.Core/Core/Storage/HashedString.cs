using System;

namespace YantraJS.Core.Core.Storage;

public readonly struct HashedString : IEquatable<HashedString>, IComparable<HashedString>
{
    public readonly StringSpan Value;

    public readonly int Hash;

    public HashedString(in StringSpan value)
    {
        Value = value;
        Hash = value.GetHashCode();
    }

    public HashedString(string value)
    {
        Value = value;
        Hash = Value.GetHashCode();
    }


    public static implicit operator HashedString(string v) => new(v);

    public static implicit operator HashedString(in StringSpan v) => new(v);


    public static bool operator ==(HashedString left, HashedString right) => left.Hash == right.Hash && left.Value == right.Value;

    public static bool operator !=(HashedString left, HashedString right) => left.Hash != right.Hash || left.Value != right.Value;

    public override bool Equals(object obj) => obj is HashedString @string && Equals(@string);

    public bool Equals(HashedString other) => Hash == other.Hash && Value == other.Value;

    public override int GetHashCode() => Hash;

    public int CompareTo(HashedString other) => Value.CompareTo(in other.Value);
    public int CompareToRef(in HashedString other) => Value.CompareTo(in other.Value);

    public override string ToString() => Value.Value;
}
