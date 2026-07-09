using System;

namespace Broiler.UI.FontDialog.Standard;

public sealed class StandardFontDialogFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiFontDialog);

    public UiElement Create(UiElementFactoryContext context) => new StandardFontDialog();
}
