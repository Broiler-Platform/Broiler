using System;
using Broiler.Documents.FormatCodes;

namespace Broiler.UI.FormatCodeView;

public sealed class FormatCodeViewSelectionChangedEventArgs : EventArgs
{
    public FormatCodeViewSelectionChangedEventArgs(int anchor, int focus)
    {
        Anchor = anchor;
        Focus = focus;
    }

    public int Anchor { get; }

    public int Focus { get; }
}

public sealed class FormatCodeNavigationRequestedEventArgs : EventArgs
{
    public FormatCodeNavigationRequestedEventArgs(
        FormatCodeMappedPosition mapping,
        FormatCodeToken? token)
    {
        Mapping = mapping;
        Token = token;
    }

    public FormatCodeMappedPosition Mapping { get; }

    public FormatCodeToken? Token { get; }
}
