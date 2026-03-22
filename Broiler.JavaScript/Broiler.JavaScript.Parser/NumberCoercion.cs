using Broiler.JavaScript.Ast.Misc;
using System;
using System.IO;
using System.Numerics;

namespace Broiler.JavaScript.Parser;

/// <summary>
/// Number parsing utilities for the lexer.
/// Extracted from Broiler.JavaScript.Core.Utils.NumberParser.
/// </summary>
internal static class NumberCoercion
{
    /// <summary>
    /// Converts a string to a number (used during lexical scanning of number tokens).
    /// </summary>
    internal static double CoerceToNumber(in StringSpan input)
    {
        // supporting ES2021 _ number separator
        var reader = new StringReader(input.Value.Replace("_", ""));

        // Skip whitespace and line terminators.
        while (IsWhiteSpaceOrLineTerminator(reader.Peek()))
            reader.Read();

        // Empty strings return 0.
        int firstChar = reader.Read();
        if (firstChar == -1)
            return 0.0;

        // The number can start with a plus or minus sign.
        bool negative = false;
        switch (firstChar)
        {
            case '-':
                negative = true;
                firstChar = reader.Read();
                break;

            case '+':
                firstChar = reader.Read();
                break;
        }

        // Infinity or -Infinity are also valid.
        if (firstChar == 'I')
        {
            string restOfString1 = reader.ReadToEnd();
            if (restOfString1.StartsWith("nfinity", StringComparison.Ordinal) == true)
            {
                // Check the end of the string for junk.
                for (int i = 7; i < restOfString1.Length; i++)
                    if (IsWhiteSpaceOrLineTerminator(restOfString1[i]) == false)
                        return double.NaN;
                
                return negative ? double.NegativeInfinity : double.PositiveInfinity;
            }
        }

        // Return NaN if the first digit is not a number or a period.
        if ((firstChar < '0' || firstChar > '9') && firstChar != '.')
            return double.NaN;

        // Parse the number.
        double result = ParseCore(reader, (char)firstChar, out ParseCoreStatus status, allowES3Octal: false);

        // Handle various error cases.
        switch (status)
        {
            case ParseCoreStatus.NoDigits:
            case ParseCoreStatus.NoExponent:
                return double.NaN;
        }

        // Check the end of the string for junk.
        string restOfString2 = reader.ReadToEnd();
        for (int i = 0; i < restOfString2.Length; i++)
            if (IsWhiteSpaceOrLineTerminator(restOfString2[i]) == false)
                return double.NaN;

        return negative ? -result : result;
    }

    private static readonly int[] integerPowersOfTen = [
        1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000
    ];

    private enum ParseCoreStatus
    {
        Success,
        NoDigits,
        NoFraction,
        NoExponent,
        ExponentHasLeadingZero,
        HexLiteral,
        ES3OctalLiteral,
        ES6OctalLiteral,
        BinaryLiteral,
        InvalidHexLiteral,
        InvalidOctalLiteral,
        InvalidBinaryLiteral,
    }

    private static double ParseCore(TextReader reader, char firstChar, out ParseCoreStatus status, bool decimalOnly = false, bool allowES3Octal = true)
    {
        double result;
        int totalDigits = 0;
        status = ParseCoreStatus.Success;

        if (firstChar == '0' && decimalOnly == false)
        {
            int c = reader.Peek();
            if (c == 'x' || c == 'X')
            {
                reader.Read();
                result = ParseHex(reader);
                if (double.IsNaN(result) == true)
                {
                    status = ParseCoreStatus.InvalidHexLiteral;
                    return double.NaN;
                }
                status = ParseCoreStatus.HexLiteral;
                return result;
            }
            else if (c == 'o' || c == 'O')
            {
                reader.Read();
                result = ParseOctal(reader);
                if (double.IsNaN(result) == true)
                {
                    status = ParseCoreStatus.InvalidOctalLiteral;
                    return double.NaN;
                }
                status = ParseCoreStatus.ES6OctalLiteral;
                return result;
            }
            else if (c == 'b' || c == 'B')
            {
                reader.Read();
                result = ParseBinary(reader);
                if (double.IsNaN(result) == true)
                {
                    status = ParseCoreStatus.InvalidBinaryLiteral;
                    return double.NaN;
                }
                status = ParseCoreStatus.BinaryLiteral;
                return result;
            }
            else if (c >= '0' && c <= '9' && allowES3Octal == true)
            {
                result = ParseOctal(reader);
                if (double.IsNaN(result) == true)
                {
                    status = ParseCoreStatus.InvalidOctalLiteral;
                    return double.NaN;
                }
                status = ParseCoreStatus.ES3OctalLiteral;
                return result;
            }
        }

        int desired1 = 0;
        int desired2 = 0;
        var desired3 = BigInteger.Zero;
        int exponentBase10 = 0;

        if (firstChar >= '0' && firstChar <= '9')
        {
            desired1 = firstChar - '0';
            totalDigits = 1;

            while (true)
            {
                int c = reader.Peek();
                if (c < '0' || c > '9')
                    break;

                reader.Read();

                if (totalDigits < 9)
                    desired1 = desired1 * 10 + (c - '0');
                else if (totalDigits < 18)
                    desired2 = desired2 * 10 + (c - '0');
                else
                    desired3 = BigInteger.Add( BigInteger.Multiply(desired3, 10), c - '0');
                
                totalDigits++;
            }
        }

        if (firstChar == '.' || reader.Peek() == '.')
        {
            if (firstChar != '.')
                reader.Read();

            int fractionalDigits = 0;
            while (true)
            {
                int c = reader.Peek();
                if (c < '0' || c > '9')
                    break;
                
                reader.Read();

                if (totalDigits < 9)
                    desired1 = desired1 * 10 + (c - '0');
                else if (totalDigits < 18)
                    desired2 = desired2 * 10 + (c - '0');
                else
                    desired3 = BigInteger.Add(BigInteger.Multiply(desired3, 10), c - '0');
                
                totalDigits++;
                fractionalDigits++;
                exponentBase10--;
            }

            if (totalDigits == 0)
            {
                status = ParseCoreStatus.NoDigits;
                return double.NaN;
            }

            if (fractionalDigits == 0)
                status = ParseCoreStatus.NoFraction;
        }

        if (reader.Peek() == 'e' || reader.Peek() == 'E')
        {
            reader.Read();

            bool exponentNegative = false;
            int c = reader.Peek();
            if (c == '+')
                reader.Read();
            else if (c == '-')
            {
                reader.Read();
                exponentNegative = true;
            }

            int firstExponentChar = reader.Read();
            int exponent = 0;
            if (firstExponentChar < '0' || firstExponentChar > '9')
            {
                status = ParseCoreStatus.NoExponent;
            }
            else
            {
                exponent = firstExponentChar - '0';
                int exponentDigits = 1;
                while (true)
                {
                    c = reader.Peek();
                    if (c < '0' || c > '9')
                        break;
                    reader.Read();
                    exponent = Math.Min(exponent * 10 + (c - '0'), 9999);
                    exponentDigits++;
                }

                if (firstExponentChar == '0' && exponentDigits > 1 && status == ParseCoreStatus.Success)
                    status = ParseCoreStatus.ExponentHasLeadingZero;
            }

            exponentBase10 += exponentNegative ? -exponent : exponent;
        }

        if (totalDigits < 16)
        {
            result = (long)desired1 * integerPowersOfTen[Math.Max(totalDigits - 9, 0)] + desired2;
        }
        else
        {
            var temp = desired3;
            desired3 = new BigInteger((long)desired1 * integerPowersOfTen[Math.Min(totalDigits - 9, 9)] + desired2);
            
            if (totalDigits > 18)
            {
                desired3 = BigInteger.Multiply(desired3, BigInteger.Pow(10, totalDigits - 18));
                desired3 = BigInteger.Add(desired3, temp);
            }

            result = (double)desired3;
        }

        if (exponentBase10 > 0)
            result *= Math.Pow(10, exponentBase10);
        else if (exponentBase10 < 0 && exponentBase10 >= -308)
            result /= Math.Pow(10, -exponentBase10);
        else if (exponentBase10 < -308)
        {
            result /= Math.Pow(10, 308);
            result /= Math.Pow(10, -exponentBase10 - 308);
        }

        if (totalDigits >= 16)
            return RefineEstimate(result, exponentBase10, desired3);

        return result;
    }

    private static double ParseHex(TextReader reader)
    {
        double result = 0;
        int digitsRead = 0;

        while (true)
        {
            int c = reader.Peek();
            if (c >= '0' && c <= '9')
                result = result * 16 + c - '0';
            else if (c >= 'a' && c <= 'f')
                result = result * 16 + c - 'a' + 10;
            else if (c >= 'A' && c <= 'F')
                result = result * 16 + c - 'A' + 10;
            else
                break;

            digitsRead++;
            reader.Read();
        }
        
        if (digitsRead == 0)
            return double.NaN;
        
        return result;
    }

    private static double ParseOctal(TextReader reader)
    {
        double result = 0;

        while (true)
        {
            int c = reader.Peek();
            if (c >= '0' && c <= '7')
                result = result * 8 + c - '0';
            else if (c == '8' || c == '9')
                return double.NaN;
            else
                break;
            
            reader.Read();
        }
        
        return result;
    }

    private static double ParseBinary(TextReader reader)
    {
        double result = 0;

        while (true)
        {
            int c = reader.Peek();
            if (c == '0')
                result = result * 2;
            else if (c == '1')
                result = result * 2 + 1;
            else if (c >= '2' && c <= '9')
                return double.NaN;
            else
                break;
            
            reader.Read();
        }
        
        return result;
    }

    private static bool IsWhiteSpaceOrLineTerminator(int c) => c == 9 || c == 0x0b || c == 0x0c || c == ' ' || c == 0xa0 || c == 0xfeff ||
            c == 0x1680 || c == 0x180e || (c >= 0x2000 && c <= 0x200a) || c == 0x202f || c == 0x205f || c == 0x3000 ||
            c == 0x0a || c == 0x0d || c == 0x2028 || c == 0x2029;

    private static double RefineEstimate(double initialEstimate, int base10Exponent, BigInteger desiredValue)
    {
        int direction = double.IsPositiveInfinity(initialEstimate) ? -1 : 1;
        int precision = 32;

        double result = initialEstimate;
        double result2 = AddUlps(result, direction);

        BigInteger multiplier = BigInteger.One;
        if (base10Exponent < 0)
            multiplier = BigInteger.Pow(10, -base10Exponent);
        else if (base10Exponent > 0)
            desiredValue = BigInteger.Multiply(desiredValue, BigInteger.Pow(10, base10Exponent));

        while (precision <= 160)
        {
            var actual1 = ScaleToInteger(result, multiplier, precision);
            var actual2 = ScaleToInteger(result2, multiplier, precision);

            var baseline = desiredValue << precision;
            var diff1 = BigInteger.Subtract(actual1, baseline);
            var diff2 = BigInteger.Subtract(actual2, baseline);

            if (diff1.Sign == direction && diff2.Sign == direction)
            {
                direction = -direction;
                result2 = AddUlps(result, direction);
            }
            else if (diff1.Sign == -direction && diff2.Sign == -direction)
            {
                result = result2;
                result2 = AddUlps(result, direction);
            }
            else
            {
                diff1 = BigInteger.Abs(diff1);
                diff2 = BigInteger.Abs(diff2);
                if (BigInteger.Compare(diff1, BigInteger.Subtract(diff2, BigInteger.One)) < 0)
                    return result;
                if (BigInteger.Compare(diff2, BigInteger.Subtract(diff1, BigInteger.One)) < 0)
                    return result2;

                precision += 32;
            }

            if (double.IsNaN(result2) == true)
                return result;
        }

        return (BitConverter.DoubleToInt64Bits(result) & 1) == 0 ? result : result2;
    }

    private static double AddUlps(double value, int ulps) =>
        BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + ulps);

    private static BigInteger ScaleToInteger(double value, BigInteger multiplier, int shift)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);

        var base2Exponent = (int)((bits & 0x7FF0000000000000) >> 52) - 1023;

        long mantissa = bits & 0xFFFFFFFFFFFFF;
        if (base2Exponent > -1023)
        {
            mantissa |= 0x10000000000000;
            base2Exponent -= 52;
        }
        else
        {
            base2Exponent -= 51;
        }

        if (bits < 0)
            mantissa = -mantissa;

        var result = new BigInteger(mantissa);
        result = BigInteger.Multiply(result, multiplier);
        shift += base2Exponent;
        result = result << shift;
        return result;
    }
}
