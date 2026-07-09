using System;

namespace Broiler.UI;

public interface IUiElementFactory
{
    Type ContractType { get; }

    UiElement Create(UiElementFactoryContext context);
}

