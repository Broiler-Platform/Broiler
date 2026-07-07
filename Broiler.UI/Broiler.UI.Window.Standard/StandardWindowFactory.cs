using System;

namespace Broiler.UI.Window.Standard;

public sealed class StandardWindowFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiWindow);

    public UiElement Create(UiElementFactoryContext context) => new StandardWindow();
}
