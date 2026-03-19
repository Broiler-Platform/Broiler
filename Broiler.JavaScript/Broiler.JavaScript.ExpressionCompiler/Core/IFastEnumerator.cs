namespace Broiler.JavaScript.ExpressionCompiler.Core;

public interface IFastEnumerator<T>
{
    bool MoveNext(out T item);

    bool MoveNext(out T item, out int index);

}


