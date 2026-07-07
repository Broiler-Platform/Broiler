using System;
using System.Collections.Generic;

namespace Broiler.UI.Standard;

public sealed class StandardDirtyRootScheduler
{
    private readonly HashSet<UiElement> _dirtyRoots = [];

    public bool HasDirtyRoots => _dirtyRoots.Count > 0;

    public IReadOnlyCollection<UiElement> DirtyRoots => [.. _dirtyRoots];

    public void Record(UiInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation.Element);
        _dirtyRoots.Add(FindRoot(invalidation.Element));
    }

    public IReadOnlyList<UiElement> ConsumeDirtyRoots()
    {
        UiElement[] roots = [.. _dirtyRoots];
        _dirtyRoots.Clear();
        return roots;
    }

    private static UiElement FindRoot(UiElement element)
    {
        UiElement root = element;
        while (root.Parent is not null)
            root = root.Parent;
        return root;
    }
}

