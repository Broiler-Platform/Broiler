using System;
using System.CodeDom.Compiler;
using System.Reflection;

namespace YantraJS.Expressions;


public class YInt32ConstantExpression(int value) : YExpression(YExpressionType.Int32Constant, typeof(int))
{
    public readonly int Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);

    private static YInt32ConstantExpression MinusOne = new(-1);

    private static YInt32ConstantExpression _0 = new(0);

    private static YInt32ConstantExpression _1 = new(1);

    private static YInt32ConstantExpression _2 = new(2);

    private static YInt32ConstantExpression _3 = new(3);

    private static YInt32ConstantExpression _4 = new(4);

    private static YInt32ConstantExpression _5 = new(5);

    private static YInt32ConstantExpression _6 = new(6);

    private static YInt32ConstantExpression _7 = new(7);

    private static YInt32ConstantExpression _8 = new(8);

    private static YInt32ConstantExpression _16 = new(16);

    private static YInt32ConstantExpression _32 = new(32);

    private static YInt32ConstantExpression _64 = new(64);

    private static YInt32ConstantExpression _128 = new(128);

    private static YInt32ConstantExpression _256 = new(256);

    private static YInt32ConstantExpression _512 = new(512);

    private static YInt32ConstantExpression _1024 = new(1024);

    internal static YInt32ConstantExpression For(int value)
    {
        switch (value)
        {
            case -1: return MinusOne;
            case 0: return _0;
            case 1: return _1;
            case 2: return _2;
            case 3: return _3;
            case 4: return _4;
            case 5: return _5;
            case 6: return _6;
            case 7: return _7;
            case 8: return _8;
            case 16: return _16;
            case 32: return _32;
            case 64: return _64;
            case 128: return _128;
            case 256: return _256;
            case 512: return _512;
            case 1024: return _1024;
        }
        return new YInt32ConstantExpression(value);
    }
}

public class YUInt32ConstantExpression(uint value) : YExpression(YExpressionType.UInt32Constant, typeof(uint))
{
    public readonly uint Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);

    private static YUInt32ConstantExpression _0 = new(0);

    private static YUInt32ConstantExpression _1 = new(1);

    private static YUInt32ConstantExpression _2 = new(2);

    private static YUInt32ConstantExpression _3 = new(3);

    private static YUInt32ConstantExpression _4 = new(4);

    private static YUInt32ConstantExpression _5 = new(5);

    private static YUInt32ConstantExpression _6 = new(6);

    private static YUInt32ConstantExpression _7 = new(7);

    private static YUInt32ConstantExpression _8 = new(8);

    private static YUInt32ConstantExpression _16 = new(16);

    private static YUInt32ConstantExpression _32 = new(32);

    private static YUInt32ConstantExpression _64 = new(64);

    private static YUInt32ConstantExpression _128 = new(128);

    private static YUInt32ConstantExpression _256 = new(256);

    private static YUInt32ConstantExpression _512 = new(512);

    private static YUInt32ConstantExpression _1024 = new(1024);

    internal static YUInt32ConstantExpression For(uint value)
    {
        switch (value)
        {
            case 0: return _0;
            case 1: return _1;
            case 2: return _2;
            case 3: return _3;
            case 4: return _4;
            case 5: return _5;
            case 6: return _6;
            case 7: return _7;
            case 8: return _8;
            case 16: return _16;
            case 32: return _32;
            case 64: return _64;
            case 128: return _128;
            case 256: return _256;
            case 512: return _512;
            case 1024: return _1024;
        }
        return new YUInt32ConstantExpression(value);
    }
}

public class YInt64ConstantExpression(long value) : YExpression(YExpressionType.Int64Constant, typeof(long))
{
    public readonly long Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YUInt64ConstantExpression(ulong value) : YExpression(YExpressionType.UInt64Constant, typeof(ulong))
{
    public readonly ulong Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YDoubleConstantExpression(double value) : YExpression(YExpressionType.DoubleConstant, typeof(double))
{
    public readonly double Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YFloatConstantExpression(float value) : YExpression(YExpressionType.FloatConstant, typeof(float))
{
    public readonly float Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YBooleanConstantExpression : YExpression
{
    public readonly bool Value;

    public static YBooleanConstantExpression True = new(true);

    public static YBooleanConstantExpression False = new(false);

    private YBooleanConstantExpression(bool value) : base(YExpressionType.BooleanConstant, typeof(bool)) => Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YByteConstantExpression(byte value) : YExpression(YExpressionType.ByteConstant, typeof(byte))
{
    public readonly byte Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}
public class YStringConstantExpression(string value) : YExpression(YExpressionType.StringConstant, typeof(string))
{
    public readonly string Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YTypeConstantExpression(Type value) : YExpression(YExpressionType.TypeConstant, typeof(Type))
{
    public readonly Type Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}

public class YMethodConstantExpression(MethodInfo value) : YExpression(YExpressionType.MethodConstant, typeof(Type))
{
    public readonly MethodInfo Value = value;

    public override void Print(IndentedTextWriter writer) => writer.Write(Value);
}
