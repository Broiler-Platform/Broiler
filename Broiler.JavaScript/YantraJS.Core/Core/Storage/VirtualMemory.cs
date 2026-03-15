using System.ComponentModel;

namespace YantraJS.Core.Core.Storage;

public struct VirtualMemory<T>
{

    private T[] nodes = null;
    private int last = 0;

    public readonly bool IsEmpty => Count == 0;

    public readonly int Count => nodes?.Length ?? 0;

    public VirtualMemory()
    {

    }

    public readonly ref T this[VirtualArray a, int index]
    {
        get
        {
            return ref nodes[a.Offset + index];
        }
    }

    [Browsable(false)]
    public readonly ref T GetAt(int index) => ref nodes[index];

    public VirtualArray Allocate(int length)
    {
        var max = last + length;
        if (nodes == null || nodes.Length <= max)
        {
            // we need to resize...
            var capacity = last * 2;
            if (capacity <= max)
            {
                capacity = ((max / 16)+ 1) * 16;
            }
            SetCapacity(capacity);
        }
        var offset = last;
        last += length;
        return new VirtualArray(offset, length);
    }

    public void SetCapacity(int max)
    {
        if (max <=0)
        {
            return;
        }
        if (nodes == null)
        {
            nodes = new T[max];
            return;
        }

        if (nodes.Length >= max)
        {
            return;
        }
        System.Array.Resize(ref nodes, max);
    }
}

public readonly struct VirtualArray(int offset, int length)
{
    public readonly int Offset = offset;
    public readonly int Length = length;

    public bool IsEmpty => Length == 0;
}
