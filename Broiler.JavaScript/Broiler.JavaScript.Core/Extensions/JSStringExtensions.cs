using System.Runtime.CompilerServices;
using YantraJS.Core;

namespace YantraJS.Extensions;

public static class JSStringExtensions
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Left(this string value, int max) => value.Length > max ? value.Substring(0, max) : value;


    public static string JSTrim(this string text) => text.Trim();

    public static string JSTrim(this JSValue text) => text.ToString().Trim();
}
