using System;

namespace Broiler.UI.Slider.Standard;

public sealed class StandardSliderFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiSlider);

    public UiElement Create(UiElementFactoryContext context) => new StandardSlider();
}
