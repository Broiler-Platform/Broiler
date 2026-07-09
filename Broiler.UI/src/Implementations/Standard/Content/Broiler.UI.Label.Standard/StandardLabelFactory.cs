using System;

namespace Broiler.UI.Label.Standard;

public sealed class StandardLabelFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiLabel);

    public UiElement Create(UiElementFactoryContext context) => new StandardLabel();
}
