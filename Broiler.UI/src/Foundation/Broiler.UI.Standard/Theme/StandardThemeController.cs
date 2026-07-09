using System;
using System.Collections.Generic;

namespace Broiler.UI.Standard;

/// <summary>
/// Applies a <see cref="StandardThemeTokens"/> palette to a live session: sets the
/// shared <see cref="StandardControlPaint"/> palette (so newly constructed controls
/// and non-themed paints follow) and re-themes every existing
/// <see cref="IStandardThemedControl"/> in the tree, then invalidates so the next
/// frame redraws. This is the render-time / live path that layers on top of the
/// construction-time default established in Phase A.
/// </summary>
public static class StandardThemeController
{
    /// <summary>Applies a palette to every root window/element in the session.</summary>
    /// <returns>The number of controls that were re-themed.</returns>
    public static int Apply(UiSession session, StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(theme);

        StandardControlPaint.ApplyTheme(theme);

        int themed = 0;
        foreach (UiElement root in session.Roots)
        {
            foreach (UiElement element in StandardTreeTraversal.PreOrder(root))
            {
                if (element is IStandardThemedControl themedControl)
                {
                    themedControl.ApplyTheme(theme);
                    themed++;
                }

                element.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            }
        }

        return themed;
    }

    /// <summary>
    /// Applies the preset selected from neutral system preferences (color scheme +
    /// contrast). Hosts can call this whenever the OS theme changes.
    /// </summary>
    public static int Apply(UiSession session, UiSystemSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Apply(session, StandardThemeTokens.Select(settings.ContrastPreference, settings.ColorScheme == UiColorScheme.Dark));
    }

    /// <summary>Convenience overload selecting a preset directly.</summary>
    public static int Apply(UiSession session, UiColorScheme scheme, UiContrastPreference contrast = UiContrastPreference.NoPreference) =>
        Apply(session, StandardThemeTokens.Select(contrast, scheme == UiColorScheme.Dark));

    /// <summary>Re-themes a single element subtree (e.g. a popup created after the last apply).</summary>
    public static void ApplyToSubtree(UiElement root, StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(theme);

        foreach (UiElement element in StandardTreeTraversal.PreOrder(root))
        {
            if (element is IStandardThemedControl themedControl)
                themedControl.ApplyTheme(theme);

            element.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }
}
