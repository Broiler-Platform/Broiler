using System;

namespace YantraJS.Core;

public readonly struct DisposableAction(Action action) : IDisposable
{
    public static readonly IDisposable Empty = new EmptyDisposable();

    public void Dispose() => action();

    private readonly struct EmptyDisposable : IDisposable
    {
        public void Dispose()
        {
            
        }
    }

    public static DisposableAction<T> Create<T>(Action<T> action, T value) => new(action, value);
}

public readonly struct DisposableAction<T>(Action<T> action, T value) : IDisposable
{
    public void Dispose() => action(value);
}
