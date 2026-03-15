using System;

namespace TheArtOfDev.HtmlRenderer.Core.Entities;

public sealed class HtmlRefreshEventArgs(bool layout) : EventArgs
{

    public bool Layout { get; } = layout;
    public override string ToString() => $"Layout: {Layout}";
}