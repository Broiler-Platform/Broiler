using System;
using Broiler.UI.Panel;

namespace Broiler.UI.Panel.Standard;

public sealed class StandardPanelFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiPanel);

    public UiElement Create(UiElementFactoryContext context) => new StandardPanel();
}
