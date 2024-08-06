using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dalamud.Utility.Text;

/// <summary>Creates a UTF-8 byte array that can be recycled in a pool.</summary>
[InterpolatedStringHandler]
public ref struct Utf8InterpolatedStringHandler
{
    private readonly IFormatProvider? provider;

    private byte[] buffer;
    private char[] buffer16;
    private int pos;
    private int cap;

    /// <summary>Initializes a new instance of the <see cref="Utf8InterpolatedStringHandler"/> struct.</summary>
    /// <param name="literalLength">Number of characters in the literal.</param>
    /// <param name="formattedCount">Number of formatted entries.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8InterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _ = formattedCount;

        // +1 for null termination
        this.buffer = ArrayPool<byte>.Shared.Rent(this.cap = Math.Max(256, literalLength + 1));
        this.buffer[0] = 0;
        this.buffer16 = [];
        this.pos = 0;
    }

    /// <summary>Initializes a new instance of the <see cref="Utf8InterpolatedStringHandler"/> struct.</summary>
    /// <param name="literalLength">Number of characters in the literal.</param>
    /// <param name="formattedCount">Number of formatted entries.</param>
    /// <param name="provider">Format provider.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8InterpolatedStringHandler(int literalLength, int formattedCount, IFormatProvider? provider)
    {
        _ = formattedCount;

        // +1 for null termination
        this.buffer = ArrayPool<byte>.Shared.Rent(this.cap = Math.Max(256, literalLength + 1));
        this.buffer[0] = 0;
        this.buffer16 = [];
        this.pos = 0;
        this.provider = provider;
    }

    /// <summary>Gets the formatted UTF-8 string.</summary>
    public readonly ReadOnlySpan<byte> Formatted => this.buffer.AsSpan(0, this.pos);

    /// <summary>Returns pooled objects to the pool.</summary>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(this.buffer);
        ArrayPool<char>.Shared.Return(this.buffer16);
        this.buffer = [];
        this.buffer16 = [];
    }

    /// <summary>Appends a value.</summary>
    /// <param name="value">Value to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(string value) => this.AppendLiteral(value.AsSpan());

    /// <summary>Appends a value.</summary>
    /// <param name="value">Value to append.</param>
    /// <param name="alignment">Minimum number of characters that should be written for this value.
    /// If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(string value, int alignment)
    {
        var startingPos = this.pos;
        this.AppendLiteral(value.AsSpan());
        this.AppendOrInsertAlignmentIfNeeded(startingPos, alignment);
    }

    /// <inheritdoc cref="AppendLiteral(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(ReadOnlySpan<char> value) =>
        Encoding.UTF8.GetBytes(value, this.Allocate(Encoding.UTF8.GetByteCount(value)));

    /// <inheritdoc cref="AppendLiteral(string, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(ReadOnlySpan<char> value, int alignment)
    {
        var startingPos = this.pos;
        Encoding.UTF8.GetBytes(value, this.Allocate(Encoding.UTF8.GetByteCount(value)));
        this.AppendOrInsertAlignmentIfNeeded(startingPos, alignment);
    }

    /// <inheritdoc cref="AppendLiteral(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(ReadOnlySpan<byte> value) => value.CopyTo(this.Allocate(value.Length));

    /// <inheritdoc cref="AppendLiteral(string, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(ReadOnlySpan<byte> value, int alignment)
    {
        var startingPos = this.pos;
        value.CopyTo(this.Allocate(value.Length));
        this.AppendOrInsertAlignmentIfNeeded(startingPos, alignment);
    }

    /// <summary>Writes the specified value to the handler.</summary>
    /// <param name="value">The value to write.</param>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    public void AppendFormatted<T>(T value) => this.AppendFormatted(value, 0, null);

    /// <summary>Writes the specified value to the handler.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    public void AppendFormatted<T>(T value, string? format) => this.AppendFormatted(value, 0, format);

    /// <summary>Writes the specified value to the handler.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="alignment">Minimum number of characters that should be written for this value.
    /// If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    public void AppendFormatted<T>(T value, int alignment) => this.AppendFormatted(value, alignment, null);

    /// <summary>Writes the specified value to the handler.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="alignment">Minimum number of characters that should be written for this value.
    /// If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
    /// <param name="format">The format string.</param>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        int written;
        switch (value)
        {
            case IUtf8SpanFormattable f:
                while (!f.TryFormat(this.buffer.AsSpan()[this.pos..(this.cap - 1)], out written, format, this.provider))
                    this.Grow();
                this.buffer[this.pos += written] = 0;
                this.AppendOrInsertAlignmentIfNeeded(this.pos - written, alignment);
                return;

            case ISpanFormattable f:
                while (!f.TryFormat(this.buffer16, out written, format, this.provider))
                {
                    var newCap = Math.Max(this.buffer16.Length * 2, 128);
                    ArrayPool<char>.Shared.Return(this.buffer16);
                    this.buffer16 = [];
                    // next line may throw; need clearing before (the line above.)
                    this.buffer16 = ArrayPool<char>.Shared.Rent(newCap);
                }

                this.AppendLiteral(this.buffer16.AsSpan(0, written), alignment);
                this.pos += written;
                this.AppendOrInsertAlignmentIfNeeded(this.pos - written, alignment);
                return;

            case IFormattable f:
                this.AppendLiteral(f.ToString(format, this.provider).AsSpan(), alignment);
                return;

            case object f:
                this.AppendLiteral(f.ToString().AsSpan(), alignment);
                return;
        }
    }

    /// <inheritdoc cref="AppendLiteral(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<char> value) => this.AppendLiteral(value);

    /// <inheritdoc cref="AppendLiteral(string, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment) => this.AppendLiteral(value, alignment);

    /// <inheritdoc cref="AppendLiteral(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<byte> value) => this.AppendLiteral(value);

    /// <inheritdoc cref="AppendLiteral(string, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted(ReadOnlySpan<byte> value, int alignment) => this.AppendLiteral(value, alignment);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Grow() => this.Grow(this.cap + 1);

    private void Grow(int reqCap)
    {
        if (reqCap > Array.MaxLength)
            throw new OutOfMemoryException();
        if (reqCap <= this.cap)
            return;
        var newCap = Math.Min(
            Math.Max(reqCap, 1 << (32 - BitOperations.LeadingZeroCount((uint)this.cap))),
            Array.MaxLength);
        var newBuf = ArrayPool<byte>.Shared.Rent(newCap);
        this.buffer.AsSpan(0, this.pos + 1).CopyTo(newBuf);
        ArrayPool<byte>.Shared.Return(this.buffer);
        this.buffer = newBuf;
        this.cap = newCap;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<byte> Allocate(int nb)
    {
        // +1 for null termination
        this.Grow(this.pos + nb + 1);

        var res = this.buffer.AsSpan(this.pos, nb);
        this.pos += nb;
        this.buffer[this.pos] = 0;
        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Return(int nb) => this.buffer[this.pos -= nb] = 0;

    private void AppendOrInsertAlignmentIfNeeded(int startingPos, int alignment)
    {
        if (alignment == 0)
            return;

        var length = this.pos - startingPos;
        var leftAlign = false;
        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        var num = alignment - length;
        if (num <= 0)
            return;

        this.Allocate(num);
        if (leftAlign)
        {
            this.buffer.AsSpan().Slice(this.pos, num).Fill((byte)' ');
        }
        else
        {
            this.buffer.AsSpan(startingPos, length).CopyTo(this.buffer.AsSpan(startingPos + num));
            this.buffer.AsSpan(startingPos, num).Fill((byte)' ');
        }

        this.pos += num;
    }
}
