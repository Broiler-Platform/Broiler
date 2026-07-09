using System;

namespace Broiler.UI.CheckBox.Standard;

public sealed class StandardCheckBoxFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiCheckBox);

    public UiElement Create(UiElementFactoryContext context) => new StandardCheckBox();
}
