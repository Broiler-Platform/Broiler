using System;

namespace Broiler.UI.ComboBox.Standard;

public sealed class StandardComboBoxFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiComboBox);

    public UiElement Create(UiElementFactoryContext context) => new StandardComboBox();
}
