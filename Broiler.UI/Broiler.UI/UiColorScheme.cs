namespace Broiler.UI;

/// <summary>
/// The neutral light/dark color-scheme preference. A host detects the operating
/// system setting and publishes it through <see cref="UiSystemSettings"/>; the
/// Standard layer maps it to a concrete palette.
/// </summary>
public enum UiColorScheme
{
    Light = 0,
    Dark,
}
