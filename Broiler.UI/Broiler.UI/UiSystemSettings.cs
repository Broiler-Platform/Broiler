namespace Broiler.UI;

public sealed record UiSystemSettings(
    UiContrastPreference ContrastPreference,
    double TextScale,
    bool ReducedMotion,
    UiFlowDirection FlowDirection)
{
    public static UiSystemSettings Default { get; } =
        new(UiContrastPreference.NoPreference, 1, ReducedMotion: false, UiFlowDirection.LeftToRight);
}
