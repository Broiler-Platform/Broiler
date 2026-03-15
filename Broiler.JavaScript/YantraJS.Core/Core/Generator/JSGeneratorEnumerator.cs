using System.Collections;
using System.Collections.Generic;

namespace YantraJS.Core.Generator;

public struct JSGeneratorEnumerator(JSGenerator g) : IEnumerator<(uint Key, JSProperty Value)>
{
    uint index = 0;

    public readonly (uint Key, JSProperty Value) Current => (index - 1, JSProperty.Property(g.value));

    readonly object IEnumerator.Current => Current;

    public readonly void Dispose()
    {

    }

    public bool MoveNext()
    {
        g.Next();
        index++;
        return !g.done;
    }

    public readonly void Reset()
    {

    }
}
