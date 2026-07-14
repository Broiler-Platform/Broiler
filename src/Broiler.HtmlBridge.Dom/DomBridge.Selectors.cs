using Broiler.Dom;
using Broiler.CSS.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Compatibility entry points over the shared canonical-DOM selector matcher.
/// </summary>
public sealed partial class DomBridge
{
    private static readonly CssSelectorMatcher SharedSelectorMatcher =
        new(new BridgeSelectorStateProvider());

    internal static bool MatchesSelector(
        DomElement element,
        string selector,
        DomElement? scope = null) =>
        SharedSelectorMatcher.Matches(element, selector, scope);

    private sealed class BridgeSelectorStateProvider : ICssSelectorStateProvider
    {
        public bool? IsChecked(DomElement element)
        {
            if (element is not DomElement bridgeElement)
                return null;

            return GetElementRuntimeState(bridgeElement).FormControl.Checked.TryGet(out var value)
                ? value is true
                : null;
        }
    }

    internal static string AsciiToLower(string input)
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
