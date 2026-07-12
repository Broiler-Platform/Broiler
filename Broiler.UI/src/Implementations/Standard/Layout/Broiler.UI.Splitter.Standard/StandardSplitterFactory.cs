using System;

namespace Broiler.UI.Splitter.Standard;

public sealed class StandardSplitterFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiSplitter);

    public UiElement Create(UiElementFactoryContext context) => new StandardSplitter();
}
