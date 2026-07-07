using System;
using System.Collections.Generic;

namespace Broiler.UI.Standard;

public sealed class StandardThemeResolver
{
    private readonly Dictionary<UiElement, StandardThemeTokens> _overrides = [];

    public StandardThemeResolver(StandardThemeTokens? defaultTokens = null)
    {
        DefaultTokens = defaultTokens ?? StandardThemeTokens.Default;
    }

    public StandardThemeTokens DefaultTokens { get; }

    public void SetOverride(UiElement element, StandardThemeTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(element);
        _overrides[element] = tokens ?? throw new ArgumentNullException(nameof(tokens));
        element.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool ClearOverride(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        bool removed = _overrides.Remove(element);
        if (removed)
            element.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return removed;
    }

    public StandardThemeTokens Resolve(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        UiElement? current = element;
        while (current is not null)
        {
            if (_overrides.TryGetValue(current, out StandardThemeTokens? tokens))
                return tokens;
            current = current.Parent;
        }

        return DefaultTokens;
    }
}

