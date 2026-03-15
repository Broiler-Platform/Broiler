using System;

namespace YantraJS.Core.Clr;

public class ClrMemberNamingConvention
{
    public readonly string Name;
    public readonly Func<StringSpan, string> Convert;

    private ClrMemberNamingConvention(string name, Func<StringSpan,string> convertName)
    {
        Convert = convertName;
        Name = name;
    }


    /// <summary>
    /// Leave clr property/method/field names as declared, this will not override JSExport
    /// </summary>
    public static ClrMemberNamingConvention Declared = new("ClrName", (x) => x.Value);

    /// <summary>
    /// Convert clr property/method/field names to camel case
    /// </summary>
    public static ClrMemberNamingConvention CamelCase
        = new("CamelCase", (x) => x.ToCamelCase());

}
