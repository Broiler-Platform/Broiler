using System;

namespace Broiler.UI.Menu.Standard;

public sealed class StandardMenuFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiMenu);

    public UiElement Create(UiElementFactoryContext context) => new StandardMenu();
}
