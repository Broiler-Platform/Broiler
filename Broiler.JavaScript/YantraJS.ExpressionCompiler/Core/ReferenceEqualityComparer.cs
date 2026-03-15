using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YantraJS;

public class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance = new();

    public new bool Equals(object x, object y) => x == y;

    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
