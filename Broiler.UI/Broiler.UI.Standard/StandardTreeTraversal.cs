using System;
using System.Collections.Generic;
using Broiler.UI;

namespace Broiler.UI.Standard;

public static class StandardTreeTraversal
{
    public static IEnumerable<UiElement> PreOrder(UiElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        yield return root;

        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in PreOrder(child))
                yield return descendant;
        }
    }

    public static IEnumerable<UiElement> PostOrder(UiElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in PostOrder(child))
                yield return descendant;
        }

        yield return root;
    }

    public static IEnumerable<UiElement> Ancestors(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        UiElement? current = element.Parent;
        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }
}

