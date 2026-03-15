using System;

namespace TheArtOfDev.HtmlRenderer.Adapters;

public abstract class RImage : IDisposable
{
    public abstract double Width { get; }
    public abstract double Height { get; }

    public abstract void Dispose();
}