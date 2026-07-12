namespace Broiler.HtmlBridge;

/// <summary>
/// Compatibility entry points over the shared canonical-DOM selector matcher.
/// </summary>
public sealed partial class DomBridge
{
    private static readonly CSS.Dom.CssSelectorMatcher SharedSelectorMatcher =
        new(new BridgeSelectorStateProvider());

    private static bool MatchesSelector(
        Broiler.Dom.DomElement element,
        string selector,
        Broiler.Dom.DomElement? scope = null) =>
        SharedSelectorMatcher.Matches(element, selector, scope);

    private sealed class BridgeSelectorStateProvider : CSS.Dom.ICssSelectorStateProvider
    {
        public bool? IsChecked(Broiler.Dom.DomElement element)
        {
            if (element is not Broiler.Dom.DomElement bridgeElement)
                return null;

            return GetElementRuntimeState(bridgeElement).FormControl.Checked.TryGet(out var value)
                ? value is true
                : null;
        }
    }

    private static string AsciiToLower(string input)
    {
        var characters = input.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] is >= 'A' and <= 'Z')
                characters[index] = (char)(characters[index] + 32);
        }
        return new string(characters);
    }
}
