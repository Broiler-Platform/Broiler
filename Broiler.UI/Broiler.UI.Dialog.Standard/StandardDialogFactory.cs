using System;

namespace Broiler.UI.Dialog.Standard;

public sealed class StandardDialogFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiDialog);

    public UiElement Create(UiElementFactoryContext context) => new StandardDialog();
}
