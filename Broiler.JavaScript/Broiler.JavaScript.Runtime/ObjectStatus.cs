namespace Broiler.JavaScript.Runtime;

public enum ObjectStatus
{
    None = 0,
    Frozen = 1,
    Sealed = 2,
    NonExtensible = 4,
    SealedOrFrozen = 3,
    SealedFrozenNonExtensible = 7
}
