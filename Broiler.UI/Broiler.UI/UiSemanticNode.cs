using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI;

public sealed record UiSemanticNode(
    UiSemanticRole Role,
    string Name,
    BRect Bounds,
    UiSemanticState State,
    IReadOnlyList<UiSemanticNode> Children,
    UiSemanticTextInfo? TextInfo = null);
