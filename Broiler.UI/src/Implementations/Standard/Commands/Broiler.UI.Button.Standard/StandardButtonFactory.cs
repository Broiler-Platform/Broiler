using System;

namespace Broiler.UI.Button.Standard;

public sealed class StandardButtonFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiButton);

    public UiElement Create(UiElementFactoryContext context) => new StandardButton();
}
