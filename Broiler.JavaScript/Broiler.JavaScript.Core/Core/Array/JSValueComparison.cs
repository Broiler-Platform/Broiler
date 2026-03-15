using System;
using System.Collections.Generic;

namespace YantraJS.Core;

internal class Comparer<T>(Comparison<T> cx) : IComparer<T>
{
    public static implicit operator Comparer<T>(Comparison<T> jv) => new(jv);

    public int Compare(T x, T y) => cx(x, y);
}
