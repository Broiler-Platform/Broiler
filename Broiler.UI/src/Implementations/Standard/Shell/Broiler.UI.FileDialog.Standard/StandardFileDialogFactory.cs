using System;

namespace Broiler.UI.FileDialog.Standard;

public sealed class StandardFileDialogFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiFileDialog);

    public UiElement Create(UiElementFactoryContext context) => new StandardFileDialog();
}
