using System;

namespace Broiler.UI.FormatCodeView.Standard;

public sealed class StandardFormatCodeViewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiFormatCodeView);

    public UiElement Create(UiElementFactoryContext context) => new StandardFormatCodeView();
}
