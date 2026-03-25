using System;
using System.Collections.Concurrent;
using System.Globalization;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Intl;

public class JSIntl
{
    [JSExportSameName]
    public static JSValue DateTimeFormat => JSEngine.ClrInterop.GetClrType(typeof(JSIntlDateTimeFormat));

    [JSExportSameName]
    public static JSValue RelativeTimeFormat => JSEngine.ClrInterop.GetClrType(typeof(JSIntlRelativeTimeFormat));

}

public class JSIntlRelativeTimeFormat(in Arguments a) : JavaScriptObject(a)
{
    [JSExport]
    public JSValue Format(in Arguments a) => a[0] ?? JSUndefined.Value;

}

public class JSIntlDateTimeFormat : JavaScriptObject
{
    private static ConcurrentDictionary<string, JSIntlDateTimeFormat> formats = new();
    private readonly CultureInfo locale;

    public static JSIntlDateTimeFormat Get(CultureInfo culture) => formats.GetOrAdd(culture.DisplayName, (key) => new JSIntlDateTimeFormat(culture));

    [JSExport]
    public JSValue Format(in Arguments a) => a[0] ?? JSUndefined.Value;

    internal JSValue Format(DateTimeOffset value, JSObject format) => new JSString(value.ToString());

    public JSIntlDateTimeFormat(in Arguments a) : base(a) { }

    internal JSIntlDateTimeFormat(CultureInfo locale) : base(Arguments.Empty) => this.locale = locale;
}
