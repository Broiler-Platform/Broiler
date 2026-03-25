using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Extensions;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using System;
using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.Engine.Core;

namespace Broiler.JavaScript.BuiltIns.Generator;

[JSClassGenerator("Generator")]
public partial class JSGenerator : JSObject, IJSGenerator
{
    readonly IElementEnumerator en;
    private ClrGeneratorV2 cg;
    private readonly string name;

    internal JSValue value;
    internal bool done;

    public JSGenerator(in Arguments a) : base(JSEngine.NewTargetPrototype) => throw new NotImplementedException();

    public JSGenerator(IElementEnumerator en, string name) : this()
    {
        this.en = en;
        this.name = name;
    }

    public JSGenerator(ClrGeneratorV2 g) : this()
    {
        cg = g;
        value = JSUndefined.Value;
    }

    public override string ToString() => $"[object {name}]";

    [JSExport("toString")]
    public new JSValue ToString(in Arguments a) => JSValue.CreateString(ToString());


    public JSValue Return(JSValue value)
    {
        done = true;
        this.value = JSUndefined.Value;

        return NewWithProperties().AddProperty(KeyStrings.value, value).AddProperty(KeyStrings.done, done ? JSValue.BooleanTrue : JSValue.BooleanFalse);
    }

    public JSValue Throw(JSValue value)
    {
        cg.InjectException(JSException.FromValue(value));
        return value;
    }

    public JSValue ValueObject => NewWithProperties().AddProperty(KeyStrings.value, value).AddProperty(KeyStrings.done, done ? JSValue.BooleanTrue : JSValue.BooleanFalse);

    public bool MoveNext(JSValue replaceOld, out JSValue item)
    {
        var c = JSEngine.Current as IJSExecutionContext;
        var top = c?.Top;

        try
        {
            // c.Top = cg.StackItem;
            cg.Next(replaceOld, out item, out done);
            value = item;

            if (!done)
                return true;

            value = item;
            done = true;

            return false;
        }
        finally
        {
            if (c != null) c.Top = top;
        }
    }

    public JSValue Next(JSValue replaceOld = null)
    {
        if (en != null)
        {
            if (en.MoveNext(out JSValue item))
            {
                value = item;
                return ValueObject;
            }

            done = true;
            value = JSUndefined.Value;

            return ValueObject;
        }

        var c = JSEngine.Current as IJSExecutionContext;
        var top = c?.Top;
        
        try
        {
            cg.Next(replaceOld, out value, out done);
            return ValueObject;
        }
        finally
        {
            if (c != null) c.Top = top;
        }
    }

    public override IElementEnumerator GetElementEnumerator() => new ElementEnumerator(this);

    private struct ElementEnumerator(JSGenerator generator) : IElementEnumerator
    {
        int index = -1;

        public bool MoveNext(out JSValue value)
        {
            generator.Next();
            if (!generator.done)
            {
                index++;
                value = generator.value;
                return true;
            }

            value = JSUndefined.Value;
            return false;
        }

        public bool MoveNext(out bool hasValue, out JSValue value, out uint index)
        {
            generator.Next();

            if (!generator.done)
            {
                this.index++;
                index = (uint)this.index;
                hasValue = true;
                value = generator.value;
                return true;
            }

            index = 0;
            value = JSUndefined.Value;
            hasValue = false;

            return false;
        }

        public bool MoveNextOrDefault(out JSValue value, JSValue @default)
        {
            generator.Next();

            if (!generator.done)
            {
                index++;
                value = generator.value;
                return true;
            }

            value = @default;
            return false;
        }

        public JSValue NextOrDefault(JSValue @default)
        {
            generator.Next();

            if (!generator.done)
            {
                index++;
                return generator.value;
            }

            return @default;
        }
    }

    [JSExport("next", Length = 1)]
    public JSValue Next(in Arguments a) => Next(a.Length == 0 ? null : a.Get1());

    [JSExport("return", Length = 1)]
    public JSValue Return(in Arguments a) => Return(a.Get1());

    [JSExport("throw", Length = 1)]
    public JSValue Throw(in Arguments a) => Throw(a.Get1());
}
