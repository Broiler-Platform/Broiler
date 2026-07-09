using System;
using Broiler.Graphics;

namespace Broiler.UI.ScrollView;

public sealed class UiScrollOffsetChangedEventArgs : EventArgs
{
    public UiScrollOffsetChangedEventArgs(BPoint oldOffset, BPoint newOffset)
    {
        OldOffset = oldOffset;
        NewOffset = newOffset;
    }

    public BPoint OldOffset { get; }

    public BPoint NewOffset { get; }
}
