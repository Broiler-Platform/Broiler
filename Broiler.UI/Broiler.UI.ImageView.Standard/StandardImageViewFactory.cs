using System;

namespace Broiler.UI.ImageView.Standard;

public sealed class StandardImageViewFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiImageView);

    public UiElement Create(UiElementFactoryContext context) => new StandardImageView();
}
