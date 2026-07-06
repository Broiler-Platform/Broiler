using System;
using Broiler.UI.ListView;

namespace Broiler.UI.ListView.Standard;

public sealed class StandardListViewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiListView);

    public UiElement Create(UiElementFactoryContext context) => new StandardListView();
}
