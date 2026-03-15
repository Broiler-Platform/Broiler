using System;

namespace Broiler.HTML.Core.Core.Entities;

public readonly struct CssBlockSelectorItem
{
    public CssBlockSelectorItem(string @class, bool directParent, bool adjacentSibling = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(@class);

        Class = @class;
        DirectParent = directParent;
        AdjacentSibling = adjacentSibling;
    }

    public readonly string Class { get; }
    public readonly bool DirectParent { get; }
    public readonly bool AdjacentSibling { get; }
    public override readonly string ToString() => Class + (DirectParent ? " > " : AdjacentSibling ? " + " : string.Empty);
}