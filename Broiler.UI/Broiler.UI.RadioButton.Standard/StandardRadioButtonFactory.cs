using System;
using Broiler.UI.RadioButton;

namespace Broiler.UI.RadioButton.Standard;

public sealed class StandardRadioButtonFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiRadioButton);

    public UiElement Create(UiElementFactoryContext context) => new StandardRadioButton();
}
