namespace Broiler.JavaScript.Storage;

public enum JSPropertyAttributes : byte
{
    Empty = 0,
    Value = 1,
    Property = 2,
    Configurable = 8,
    Enumerable = 16,
    Readonly = 32,
    // Deleted = 64,

    // shortcuts..
    EnumerableConfigurableValue = Value | Enumerable | Configurable,
    EnumerableConfigurableReadonlyValue = Value | Enumerable | Configurable | Readonly,
    ConfigurableValue = Value | Configurable,
    ConfigurableReadonlyValue = Value | Configurable | Readonly,

    EnumerableConfigurableProperty = Property | Enumerable | Configurable,
    EnumerableConfigurableReadonlyProperty = Property | Enumerable | Configurable | Readonly,
    ConfigurableProperty = Property | Configurable,
    ConfigurableReadonlyProperty = Property | Configurable | Readonly,

    ReadonlyValue = Readonly | Value,
    ReadonlyProperty = Readonly | Property,

    EnumerableReadonlyValue = Enumerable | Readonly | Value,
    EnumerableReadonlyProperty = Enumerable | Readonly | Property
}
