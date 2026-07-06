using System.Collections.Generic;

namespace Broiler.UI;

public interface IUiAccessibilityHost
{
    void PublishSemanticSnapshot(IReadOnlyList<UiSemanticNode> roots);

    void NotifySemanticChanged(UiElement element, UiSemanticChangeKind change);
}
