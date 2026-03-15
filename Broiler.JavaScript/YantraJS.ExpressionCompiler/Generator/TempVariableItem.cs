using System;
using System.Reflection.Emit;
using YantraJS.Core;

namespace YantraJS.Generator;


public class TempVariables(ILWriter il) : LinkedStack<TempVariables.TempVariableItem>
{
    public TempVariableItem Push() => Push(new TempVariableItem(il));

    public LocalBuilder this[Type type]
    {
        get
        {
            return Top.Get(type);
        }
    }

    public class TempVariableItem(ILWriter writer) : LinkedStackItem<TempVariableItem>
    {
        private Sequence<IDisposable> disposables = [];

        public LocalBuilder Get(Type type)
        {
            var t = writer.NewTemp(type);
            disposables.Add(t);
            return t.Local;
        }

        public override void Dispose()
        {
            base.Dispose();
            var en = disposables.GetFastEnumerator();
            while(en.MoveNext(out var d))
                d.Dispose();
        }
    }

}
