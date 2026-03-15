using YantraJS.Core.Clr;
using YantraJS.Core.Core.Storage;

namespace YantraJS.Core;

public partial class JSSymbol
{

    [JSExport("asyncDispose")]
    public static JSSymbol asyncDispose = new("@asyncDispose");


    [JSExport("dispose")]
    public static JSSymbol dispose = new("@dispose");

    [JSExport("asyncIterator")]
    public static JSSymbol asyncIterator = new("Symbol.asyncIterator");

    [JSExport("hasInstance")]
    public static JSSymbol hasInstance = new("Symbol.hasInstance");

    [JSExport("isConcatSpreadable")]
    public static JSSymbol isConcatSpreadable = new("Symbol.isConcatSpreadable");

    [JSExport("iterator")]
    public static JSSymbol iterator = new("Symbol.iterator");

    [JSExport("match")]
    public static JSSymbol match = new("Symbol.matchAll");

    [JSExport("replace")]
    public static JSSymbol replace = new("Symbol.replace");

    [JSExport("search")]
    public static JSSymbol search = new("Symbol.search");

    [JSExport("species")]
    public static JSSymbol species = new("Symbol.species");

    [JSExport("split")]
    public static JSSymbol split = new("Symbol.split");

    [JSExport("toPrimitive")]
    public static JSSymbol toPrimitive = new("Symbol.toPrimitive");

    [JSExport("toStringTag")]
    public static JSSymbol toStringTag = new("Symbol.toStringTag");

    [JSExport("unscopables")]
    public static JSSymbol unscopables = new("Symbol.unscopables");

    private static ConcurrentStringMap<JSSymbol> globals
        = ConcurrentStringMap<JSSymbol>.Create();

    public static JSSymbol GlobalSymbol(string name)
    {
        name = name.TrimStart('@');
        var f = typeof(JSSymbol).GetField(name);
        return (JSSymbol)f.GetValue(null);
    }

    [JSExport("for")]
    public static JSValue For(in Arguments a)
    {
        var name = a.Get1().ToString();
        return globals.GetOrCreate(name, (x) => new JSSymbol(x.Value));
    }
}
