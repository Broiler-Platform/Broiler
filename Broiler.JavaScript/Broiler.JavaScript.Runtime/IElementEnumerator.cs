namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Enumerator protocol for iterating over JavaScript object elements.
/// </summary>
public interface IElementEnumerator
{
    bool MoveNext(out bool hasValue, out JSValue value, out uint index);
    bool MoveNext(out JSValue value);
    bool MoveNextOrDefault(out JSValue value, JSValue @default);
    JSValue NextOrDefault(JSValue @default);
}
