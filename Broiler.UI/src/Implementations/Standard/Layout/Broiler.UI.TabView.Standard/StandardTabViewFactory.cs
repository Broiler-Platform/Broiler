using System;

namespace Broiler.UI.TabView.Standard;

public sealed class StandardTabViewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiTabView);

    public UiElement Create(UiElementFactoryContext context) => new StandardTabView();
}
