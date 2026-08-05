using System;

namespace Broiler.UI.TreeView.Standard;

public sealed class StandardTreeViewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiTreeView);

    public UiElement Create(UiElementFactoryContext context) => new StandardTreeView();
}
