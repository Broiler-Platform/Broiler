using System;
using System.Runtime.CompilerServices;

namespace YantraJS;

public class ScopedStack<T>
{
    public ScopedItem Top { get; private set; }

    public T TopItem => Top.Item;

    public ScopedItem Push(T item) => new(item, this);

    public class ScopedItem: IDisposable
    {
        public readonly T Item;
        public readonly ScopedItem Parent;
        private readonly ScopedStack<T> owner;

        public ScopedItem(T item, ScopedStack<T> owner)
        {
            Item = item;
            this.owner = owner;
            Parent = owner.Top;
            owner.Top = this;
        }

        public void Dispose() => owner.Top = Parent;
    }
}


public class LinkedStack<T>
    where T : LinkedStackItem<T>
{

    internal T _Top = null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Push(T item)
    {
        item.Parent = _Top;
        _Top = item;
        item.stack = this;
        // item.Init();
        return item;
    }

    public T Top
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _Top;
    }

    public T Switch(T top)
    {
        var current = _Top;
        _Top = top;
        return current;
    }

}
