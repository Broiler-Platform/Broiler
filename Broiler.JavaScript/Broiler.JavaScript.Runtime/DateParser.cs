using System;
using System.Globalization;

namespace Broiler.JavaScript.Runtime;

public static class DateParser
{
    internal static readonly string[] DefaultFormats = [
        "yyyy-MM-ddTHH:mm:ss.FFF",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-dd",
        "yyyy-MM",
        "yyyy"
    ];

    internal static readonly string[] SecondaryFormatsUTC = [
        "d MMMM yyyy HH:mm \\U\\T\\CK",
        "MMMM dd, yyyy, HH:mm:ss \\U\\T\\CK",
        "MMMM dd, yyyy HH:mm:ss \\U\\T\\CK",
    ];

    internal static readonly string[] SecondaryFormats = [
        // Formats used in DatePrototype toString methods
        "ddd MMM dd yyyy HH:mm:ss 'GMT'K",
        "ddd MMM dd yyyy",
        // ES Date Format
        "MMMM dd, yyyy HH:mm:ss \\G\\M\\TK",
        "MMMM dd, yyyy, HH:mm:ss \\G\\M\\TK",
        "d MMMM yyyy HH:mm:ss \\G\\M\\TK",
        "HH:mm:ss 'GMT'K",

        // standard formats
         "yyyy-M-dTH:m:s.FFFK",
        "yyyy/M/dTH:m:s.FFFK",
        "yyyy-M-dTH:m:sK", 
         "yyyy/M/dTH:m:sK",
         "yyyy-M-d H:m:s.FFFK",
         "yyyy/M/d H:m:s.FFFK",
         "yyyy-M-d H:m:sK",
        "yyyy/M/d H:m:sK",
        "yyyy-M-d H:mK",
        "yyyy/M/d H:mK",
        "yyyy-M-dK",
        "yyyy/M/dK",
        "yyyy-MK",
        "yyyy/MK",
        "yyyyK",
        "THH:mm:ss.FFFK",
        "THH:mm:ssK",
        "THHK",
        "yyyyTH:m"

    ];

    internal static DateTimeOffset Parse(string text)
    {
        if (DateTimeOffset.TryParseExact(text, DefaultFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
            return result;

        if (DateTimeOffset.TryParseExact(text, SecondaryFormatsUTC, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
            return result;

        if (DateTimeOffset.TryParseExact(text, SecondaryFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
            return result;

        if (DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result))
            return result;

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
            // unrecognized dates should return NaN (15.9.4.2)
            return DateTimeOffset.MinValue;

        return result;
    }
}
