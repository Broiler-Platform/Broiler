using System;
using Broiler.UI.Slider;

namespace Broiler.UI.Slider.Standard;

public sealed class StandardSliderFactory : IUiElementFactory
{
    public Type ContractType => typeof(UiSlider);

    public UiElement Create(UiElementFactoryContext context) => new StandardSlider();
}
