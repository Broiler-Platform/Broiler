using System;
using Broiler.UI.Toolbar;

namespace Broiler.UI.Toolbar.Standard;

public sealed class StandardToolbarFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiToolbar);

    public UiElement Create(UiElementFactoryContext context) => new StandardToolbar();
}
