using System;
using Broiler.UI.ScrollView;

namespace Broiler.UI.ScrollView.Standard;

public sealed class StandardScrollViewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiScrollView);

    public UiElement Create(UiElementFactoryContext context) => new StandardScrollView();
}
