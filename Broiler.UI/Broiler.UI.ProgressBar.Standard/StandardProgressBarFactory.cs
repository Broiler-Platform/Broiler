using System;
using Broiler.UI.ProgressBar;

namespace Broiler.UI.ProgressBar.Standard;

public sealed class StandardProgressBarFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiProgressBar);

    public UiElement Create(UiElementFactoryContext context) => new StandardProgressBar();
}
