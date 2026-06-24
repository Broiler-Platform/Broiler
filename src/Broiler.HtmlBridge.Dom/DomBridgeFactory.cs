namespace Broiler.HtmlBridge;

public sealed class DomBridgeFactory : IDomBridgeRuntimeFactory
{
    public IDomBridgeRuntime Create() => new DomBridge();
}
