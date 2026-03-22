using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.Ast.Misc;


[DebuggerDisplay("{Value}")]
public readonly struct StringSpan : IEquatable<StringSpan>, IEquatable<string>, IEnumerable<char>
{
    public static readonly StringSpan Empty = string.Empty;

    public readonly string? Source;
    public readonly int Offset;
    public readonly int Length;

    public StringSpan(string? source)
    {
        Source = source;
        Offset = 0;
        Length = source?.Length ?? 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StringSpan(string? buffer, int offset, int length)
    {
        if (buffer == null || (uint)offset > (uint)buffer.Length || (uint)length > (uint)(buffer.Length - offset))
            throw new InvalidOperationException($"offset/length represents invalid string or string is null");

        Source = buffer;
        Offset = offset;
        Length = length;
    }

    public string? Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Source == null)
                return Source;

            if (Offset == 0 && Length == Source.Length)
                return Source;

            return Source.Substring(Offset, Length);
        }
    }

    public unsafe char this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Source == null || (uint)index >= (uint)Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            fixed (char* src = Source)
            {
                char* charAt = src + Offset + index;
                return *charAt;
            }
        }
    }

    public static string Concat(in StringSpan a, in StringSpan b)
    {
        var alen = a.Length;
        var blen = b.Length;
        var n = alen + blen;
        var sb = new StringBuilder(n);

        sb.Append(a.Value, a.Offset, a.Length);
        sb.Append(b.Value, b.Offset, b.Length);

        return sb.ToString();
    }

    public static string Concat(in StringSpan a, string value)
    {
        var alen = a.Length;
        var n = alen + value.Length;
        var sb = new StringBuilder(n);

        sb.Append(a.Source, a.Offset, a.Length);
        sb.Append(value);

        return sb.ToString();
    }

    public static int Compare(in StringSpan a, in StringSpan b, StringComparison comparisonType)
    {
        int minLength = Math.Min(a.Length, b.Length);
        int diff = string.Compare(a.Source, a.Offset, b.Source, b.Offset, minLength, comparisonType);

        if (diff == 0)
            diff = a.Length - b.Length;

        return diff;
    }

    public override bool Equals(object? obj) => obj is not null && obj is StringSpan segment && Equals(in segment, StringComparison.Ordinal);

    public ReadOnlySpan<char> AsSpan() => Source.AsSpan().Slice(Offset, Length);

    public override string ToString() => Value ?? string.Empty;

    public StringSpan Trim() => TrimStart().TrimEnd();

    public unsafe StringSpan TrimStart()
    {
        int offset = Offset;
        int length = Length;

        if (length == 0)
            return this;

        fixed (char* src = Source)
        {
            char* start = src + offset;
            int currentLength = length;

            for (int i = 0; i < currentLength; i++)
            {
                if (!char.IsWhiteSpace(*start))
                    break;

                offset++;
                length--;
                start++;
            }
        }

        return new StringSpan(Source, offset, length);
    }

    public unsafe StringSpan TrimEnd()
    {
        int offset = Offset;
        int length = Length;

        if (length == 0)
            return this;

        fixed (char* src = Source)
        {
            char* start = src + offset + length - 1;
            int currentLength = length;

            for (int i = 0; i < currentLength; i++)
            {
                if (!char.IsWhiteSpace(*start))
                    break;

                length--;
                start--;
            }
        }

        return new StringSpan(Source, offset, length);
    }

    public string ToLower()
    {
        var length = Length;

        if (length == 0)
            return string.Empty;

        return Value!.ToLower();
    }

    public unsafe string ToCamelCase()
    {
        var length = Length;

        if (length == 0)
            return string.Empty;

        var d = new char[length];

        fixed (char* start = Source)
        {
            char* startOffset = start + Offset;
            int i;

            for (i = 0; i < length; i++)
            {
                var ch = *startOffset++;
                d[i] = char.ToLower(ch);

                if (!char.IsUpper(ch))
                {
                    i++;
                    break;
                }
            }

            for (; i < length; i++)
                d[i] = *startOffset++;
        }

        return new string(d);
    }

    public bool IsNullOrWhiteSpace() => Source == null || string.IsNullOrWhiteSpace(Value);

    public bool IsEmpty => Length == 0;

    public static implicit operator StringSpan(string source) => new(source);

    public static bool operator ==(in StringSpan left, in StringSpan right) => left.Equals(in right, StringComparison.Ordinal);
    public static bool operator !=(in StringSpan left, in StringSpan right) => !left.Equals(in right, StringComparison.Ordinal);

    public static StringSpan operator +(in StringSpan left, in StringSpan right) => new(left.Value + right.Value);

    public static StringSpan operator +(double left, in StringSpan right) => new(left.ToString() + right.Value);


    public bool Equals(StringSpan other) => Equals(in other, StringComparison.Ordinal);

    public unsafe bool StartsWith(StringSpan other)
    {
        var length = other.Length;
        if (length > Length)
            return false;

        fixed (char* start = Source)
        {
            char* startOffset = start + Offset;
            fixed (char* otherSource = other.Source)
            {
                char* otherOffset = otherSource + other.Offset;
                for (int i = 0; i < length; i++)
                {
                    if (*startOffset != *otherOffset)
                        return false;

                    startOffset++;
                    otherOffset++;
                }
            }
        }

        return true;
    }

    public bool Equals(in StringSpan other, StringComparison comparisonType)
    {
        if (Length != other.Length)
            return false;

        return string.Compare(Source, Offset, other.Source, other.Offset, other.Length, comparisonType) == 0;
    }

    public static bool Equals(in StringSpan a, in StringSpan b, StringComparison comparisonType) => a.Equals(in b, comparisonType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(string? other) => Equals(other, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(string? text, StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(text);

        int textLength = text.Length;
        if (Source == null || Length != textLength)
            return false;

        return string.Compare(Source, Offset, text, 0, textLength, comparisonType) == 0;
    }

    public int CompareTo(in StringSpan other)
    {
        if (Source == null)
        {
            if (other.Source == null)
                return 0;

            return 1;
        }

        if (other.Source == null)
            return -1;

        return string.Compare(Source, Offset, other.Source, other.Offset, Length > other.Length ? Length : other.Length, StringComparison.Ordinal);
    }

    public override int GetHashCode() => Source.UnsafeGetHashCode(Offset, Length);

    public StringSpanReader Reader() => new(this);
    
    public StringSpan Substring(int index) => new(Source, Offset + index, Length - index);

    public CharEnumerator GetEnumerator() => new(this);


    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<char> IEnumerable<char>.GetEnumerator() => GetEnumerator();

    public unsafe byte[] Encode(Encoding encoding)
    {
        if (IsEmpty)
            return [];

        fixed (char* source = Source)
        {
            char* start = source + Offset;
            var len = encoding.GetByteCount(start, Length);
            var buffer = new byte[len];

            fixed (byte* buf = buffer)
                encoding.GetBytes(start, Length, buf, buffer.Length);

            return buffer;
        }
    }

    public struct CharEnumerator(StringSpan span) : IEnumerator<char>
    {
        private int index = -1;

        public unsafe bool MoveNext(out char ch)
        {
            index++;
            if (index >= span.Length)
            {
                ch = '\0';
                return false;
            }

            fixed (char* start = span.Source)
            {
                char* ch1 = start + (span.Offset + index);
                ch = *ch1;

                return true;
            }

        }

        public readonly char Current => UnsafeChar();

        private readonly unsafe char UnsafeChar()
        {
            fixed (char* start = span.Source)
            {
                char* ch = start + (span.Offset + index);
                return *ch;
            }
        }

        readonly object IEnumerator.Current => Current;

        public readonly void Dispose() { }

        public bool MoveNext() => ++index < span.Length;

        public readonly void Reset() { }
    }
}
