using System;
using System.Collections.Generic;

namespace Broiler.UI.Standard;

public sealed record StandardSemanticSnapshot(IReadOnlyList<UiSemanticNode> Roots)
{
    public static StandardSemanticSnapshot Capture(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var roots = new List<UiSemanticNode>(session.Roots.Count);
        foreach (UiElement root in session.Roots)
            roots.Add(root.GetSemanticNode());
        return new StandardSemanticSnapshot(roots);
    }
}

