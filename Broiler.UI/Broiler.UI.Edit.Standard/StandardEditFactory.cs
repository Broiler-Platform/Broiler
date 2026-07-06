using System;
using Broiler.UI.Edit;

namespace Broiler.UI.Edit.Standard;

public sealed class StandardEditFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiEdit);

    public UiElement Create(UiElementFactoryContext context) => new StandardEdit();
}
