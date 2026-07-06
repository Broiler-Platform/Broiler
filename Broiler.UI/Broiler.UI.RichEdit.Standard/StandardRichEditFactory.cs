using System;

namespace Broiler.UI.RichEdit.Standard;

public sealed class StandardRichEditFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiRichEdit);

    public UiElement Create(UiElementFactoryContext context) => new StandardRichEdit();
}
