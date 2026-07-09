using System;

namespace Broiler.UI.ToggleButton.Standard;

public sealed class StandardToggleButtonFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiToggleButton);

    public UiElement Create(UiElementFactoryContext context) => new StandardToggleButton();
}
