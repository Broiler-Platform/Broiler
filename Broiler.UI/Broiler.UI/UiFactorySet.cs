using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.UI;

public sealed class UiFactorySet
{
    private readonly Dictionary<Type, IUiElementFactory> _factories;

    public UiFactorySet(IEnumerable<IUiElementFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);

        _factories = new Dictionary<Type, IUiElementFactory>();
        foreach (IUiElementFactory factory in factories)
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (!_factories.TryAdd(factory.ContractType, factory))
                throw new InvalidOperationException($"Duplicate UI factory for {factory.ContractType.FullName}.");
        }
    }

    public IReadOnlyCollection<IUiElementFactory> Factories => _factories.Values.ToArray();

    public bool TryGetFactory(Type contractType, out IUiElementFactory factory) =>
        _factories.TryGetValue(contractType, out factory!);

    public UiElement Create(Type contractType, UiElementFactoryContext context)
    {
        if (!_factories.TryGetValue(contractType, out IUiElementFactory? factory))
            throw new InvalidOperationException($"No UI factory exists for {contractType.FullName}.");

        return factory.Create(context);
    }
}

