using System.Text;

using Dalamud.Game.Text.SeStringHandling.SeStringSpan;

namespace Dalamud.Game.Text.SeStringHandling;

/// <summary>Utilities for implementing SeString spans.</summary>
public static class SeStringExpressionUtilities
{
    /// <summary>Parses the length of an expression.</summary>
    /// <param name="span">The byte span to parse from.</param>
    /// <param name="length">The consumed length.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool TryDecodeLength(ReadOnlySpan<byte> span, out int length)
    {
        if (span.IsEmpty)
        {
            length = 0;
            return false;
        }

        return TryDecodeUInt(span, out _, out length)
               || TryDecodeString(span, out _, out length)
               || TryDecodeNullary(span, out _, out length)
               || TryDecodeUnary(span, out _, out _, out length)
               || TryDecodeBinary(span, out _, out _, out _, out length);
    }

    /// <summary>Parses an integer expression.</summary>
    /// <param name="span">The byte span to parse from.</param>
    /// <param name="value">The parsed value.</param>
    /// <param name="length">The consumed length.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool TryDecodeUInt(ReadOnlySpan<byte> span, out uint value, out int length)
    {
        value = 0;
        switch (span.IsEmpty ? (byte)0 : span[0])
        {
            case > 0 and < 0xD0:
                value = (uint)span[0] - 1;
                length = 1;
                return true;

            case >= 0xF0 and <= 0xFE when span.Length >= 2:
            {
                var typeByte = (span[0] + 1) & 0xF;
                value = 0;
                length = 1;

                if ((typeByte & 8) != 0)
                {
                    if (span.Length <= length)
                        return false;
                    var u = (uint)span[length++];
                    if (u == 0)
                        return false;
                    value |= u << 24;
                }

                if ((typeByte & 4) != 0)
                {
                    if (span.Length <= length)
                        return false;
                    var u = (uint)span[length++];
                    if (u == 0)
                        return false;
                    value |= u << 16;
                }

                if ((typeByte & 2) != 0)
                {
                    if (span.Length <= length)
                        return false;
                    var u = (uint)span[length++];
                    if (u == 0)
                        return false;
                    value |= u << 8;
                }

                if ((typeByte & 1) != 0)
                {
                    if (span.Length <= length)
                        return false;
                    var u = (uint)span[length++];
                    if (u == 0)
                        return false;
                    value |= u;
                }

                return true;
            }

            default:
                value = 0;
                length = 0;
                return false;
        }
    }

    /// <inheritdoc cref="TryDecodeUInt"/>
    public static bool TryDecodeInt(ReadOnlySpan<byte> span, out int value, out int length)
    {
        if (!TryDecodeUInt(span, out var u32, out length))
        {
            value = 0;
            return false;
        }

        value = unchecked((int)u32);
        return true;
    }

    /// <summary>Parses a SeString expression.</summary>
    /// <param name="span">The byte span to parse from.</param>
    /// <param name="value">The parsed value.</param>
    /// <param name="length">The consumed length.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool TryDecodeString(ReadOnlySpan<byte> span, out SeStringReadOnlySpan value, out int length)
    {
        value = default;
        length = 0;
        if (span.Length < 2 || span[0] != 0xFF)
            return false;
        if (!TryDecodeInt(span[1..], out var strLen, out var exprLen)
            || strLen < 0)
            return false;

        length = 1 + exprLen + strLen;
        if (length > span.Length)
            return false;

        value = new(span.Slice(1 + exprLen, strLen));
        return true;
    }

    /// <summary>Parses a placeholder expression.</summary>
    /// <param name="span">The byte span to parse from.</param>
    /// <param name="expressionType">The parsed expression type.</param>
    /// <param name="length">The consumed length.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool TryDecodeNullary(ReadOnlySpan<byte> span, out byte expressionType, out int length)
    {
        expressionType = span.IsEmpty ? (byte)0 : span[0];
        length = expressionType is >= 0xD0 and <= 0xDF or 0xEC ? 1 : 0;
        return length != 0;
    }

    /// <summary>Parses a parameter expression.</summary>
    /// <param name="span">The byte span to parse from.</param>
    /// <param name="expressionType">The parsed expression type.</param>
    /// <param name="operand">The parsed operand.</param>
    /// <param name="length">The consumed length.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool TryDecodeUnary(
        ReadOnlySpan<byte> span,
        out byte expressionType,
        out SeExpressionReadOnlySpan operand,
        out int length)
    {
        expressionType = span.IsEmpty ? (byte)0 : span[0];
        if (expressionType is >= 0xE8 and <= 0xEB)
        {
            if (!TryDecodeLength(span[1..], out length))
            {
                operand = default;
                return false;
            }

            operand = span.Slice(1, length);
            length++;
            return true;
        }

        operand = default;
        length = 0;
        return false;
    }

    /// <summary>Parses a binary expression.</summary>
    /// <param name="span">The byte span to parse from.</param>
    /// <param name="expressionType">The parsed expression type.</param>
    /// <param name="operand1">The parsed operand 1.</param>
    /// <param name="operand2">The parsed operand 2.</param>
    /// <param name="length">The consumed length.</param>
    /// <returns><c>true</c> on success.</returns>
    public static bool TryDecodeBinary(
        ReadOnlySpan<byte> span,
        out byte expressionType,
        out SeExpressionReadOnlySpan operand1,
        out SeExpressionReadOnlySpan operand2,
        out int length)
    {
        expressionType = span.IsEmpty ? (byte)0 : span[0];
        if (expressionType is >= 0xE0 and <= 0xE5)
        {
            span = span[1..];
            if (!TryDecodeLength(span, out length))
            {
                operand1 = operand2 = default;
                return false;
            }

            operand1 = span[..length];
            span = span[length..];

            if (!TryDecodeLength(span, out var length2))
            {
                operand2 = default;
                return false;
            }

            operand2 = span[..length2];
            length = 1 + length + length2;
            return true;
        }

        operand1 = operand2 = default;
        length = 0;
        return false;
    }

    /// <summary>Calculates the number of bytes required to encode the given value as a SeString expression.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The required number of bytes.</returns>
    public static int CalculateLengthUInt(uint value) => Lumina.Text.Expressions.IntegerExpression.CalculateSize(value);

    /// <summary>Calculates the number of bytes required to encode the given value as a SeString expression.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The required number of bytes.</returns>
    public static int CalculateLengthInt(int value) =>
        Lumina.Text.Expressions.IntegerExpression.CalculateSize(unchecked((uint)value));

    /// <summary>Calculates the number of bytes required to encode the given value as a SeString expression.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The required number of bytes.</returns>
    public static int CalculateLengthString(SeStringReadOnlySpan value) =>
        1 + CalculateLengthInt(value.Body.Length) + value.Body.Length;

    /// <summary>Calculates the number of bytes required to encode the given value as a SeString expression.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The required number of bytes.</returns>
    public static int CalculateLengthString(ReadOnlySpan<char> value)
    {
        var len8 = Encoding.UTF8.GetByteCount(value);
        return 1 + CalculateLengthInt(len8) + len8;
    }

    /// <summary>Writes the given byte to the beginning of the span.</summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="b">The byte.</param>
    /// <returns>The remainder of the span.</returns>
    public static Span<byte> WriteRaw(Span<byte> span, byte b)
    {
        span[0] = b;
        return span[1..];
    }

    /// <summary>Writes the given bytes to the beginning of the span.</summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="b">The bytes.</param>
    /// <returns>The remainder of the span.</returns>
    public static Span<byte> WriteRaw(Span<byte> span, ReadOnlySpan<byte> b)
    {
        b.CopyTo(span);
        return span[b.Length..];
    }

    /// <summary>Encodes the given value to the beginning of the span.</summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns>The remainder of the span.</returns>
    public static Span<byte> EncodeUInt(Span<byte> span, uint value)
    {
        if (value < 0xCF)
        {
            span[0] = (byte)(1 + value);
            return span[1..];
        }

        var ptr = 1;
        span[0] = 0xF0;

        var t = (byte)(value >> 24);
        if (t != 0)
        {
            span[ptr] = t;
            span[0] |= 8;
            ptr++;
        }

        t = (byte)(value >> 16);
        if (t != 0)
        {
            span[ptr] = t;
            span[0] |= 4;
            ptr++;
        }

        t = (byte)(value >> 8);
        if (t != 0)
        {
            span[ptr] = t;
            span[0] |= 2;
            ptr++;
        }

        t = (byte)(value >> 0);
        if (t != 0)
        {
            span[ptr] = t;
            span[0] |= 1;
            ptr++;
        }

        span[0]--;
        return span[ptr..];
    }

    /// <summary>Encodes the given value to the beginning of the span.</summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns>The remainder of the span.</returns>
    public static Span<byte> EncodeInt(Span<byte> span, int value) => EncodeUInt(span, (uint)value);

    /// <summary>Encodes the given value to the beginning of the span.</summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns>The remainder of the span.</returns>
    public static Span<byte> EncodeString(Span<byte> span, SeStringReadOnlySpan value)
    {
        span = WriteRaw(span, 0xFF);
        span = EncodeInt(span, value.Body.Length);
        span = WriteRaw(span, value.Body);
        return span;
    }

    /// <summary>Encodes the given value to the beginning of the span.</summary>
    /// <param name="span">The span to write to.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns>The remainder of the span.</returns>
    public static Span<byte> EncodeString(Span<byte> span, ReadOnlySpan<char> value)
    {
        span = WriteRaw(span, 0xFF);
        var bc = Encoding.UTF8.GetByteCount(value);
        span = EncodeInt(span, bc);
        if (Encoding.UTF8.GetBytes(value, span[..bc]) != bc)
            throw new ArgumentException("Destination is too short.", nameof(span));
        return span[bc..];
    }
}
