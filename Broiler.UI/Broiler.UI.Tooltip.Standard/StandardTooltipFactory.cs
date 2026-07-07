using System;

namespace Broiler.UI.Tooltip.Standard;

public sealed class StandardTooltipFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiTooltip);

    public UiElement Create(UiElementFactoryContext context) => new StandardTooltip();
}
