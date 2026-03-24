using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Parser;

public static class CharExtensions
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FromCodePoint(this int cp) => char.ConvertFromUtf32(cp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HexValue(this char ch)
    {
        if (ch >= 'A')
        {
            if (ch >= 'a')
            {
                if (ch <= 'h')
                    return ch - 'a' + 10;
            }
            else if (ch <= 'H')
            {
                return ch - 'A' + 10;
            }
        }
        else if (ch <= '9')
        {
            return ch - '0';
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDigitPart(this char ch, bool hex, bool binary)
    {
        switch (ch)
        {
            case '_':
            case '0':
            case '1':
                return true;
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                if (binary)
                    return false;

                return true;
            case 'a':
            case 'b':
            case 'c':
            case 'd':
            case 'e':
            case 'f':
            case 'A':
            case 'B':
            case 'C':
            case 'D':
            case 'E':
            case 'F':
                return hex;
        }
        return false;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsIdentifierStart(this char ch)
    {
        return ch switch
        {
            '_' or '$' or '@' => true,
            _ => char.IsLetter(ch),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsIdentifierPart(this char ch)
    {
        return ch switch
        {
            '_' or '$' or '@' or '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' => true,
            _ => char.IsLetter(ch),
        };
    }
}
