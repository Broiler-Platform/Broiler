namespace Broiler.UI.Standard;

/// <summary>
/// Implemented by standard controls that can re-derive their color roles from a
/// <see cref="StandardThemeTokens"/> set. <see cref="StandardThemeController"/>
/// calls <see cref="ApplyTheme"/> across a session's tree so a running UI can
/// switch themes live (light ↔ dark ↔ high-contrast) without rebuilding controls.
/// </summary>
public interface IStandardThemedControl
{
    /// <summary>
    /// Re-assigns this control's theme-derived color properties from the supplied
    /// token set. Any application override set after the last call is replaced, so
    /// re-apply overrides after switching themes if needed.
    /// </summary>
    void ApplyTheme(StandardThemeTokens theme);
}
