using System;
using Broiler.UI;

namespace Broiler.UI.Standard;

public sealed class StandardUiSessionBuilder
{
    private IUiDispatcher? _dispatcher;
    private IUiClock? _clock;
    private UiFactorySet? _factories;

    public StandardUiSessionBuilder WithDispatcher(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        return this;
    }

    public StandardUiSessionBuilder WithClock(IUiClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        return this;
    }

    public StandardUiSessionBuilder WithFactories(UiFactorySet factories)
    {
        _factories = factories ?? throw new ArgumentNullException(nameof(factories));
        return this;
    }

    public UiSession Build(IUiHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new UiSession(
            host,
            _dispatcher ?? new ImmediateUiDispatcher(),
            _clock ?? new StandardUiClock(),
            _factories);
    }
}

