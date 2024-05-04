using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads;

/// <summary>Represents a text payload.</summary>
public sealed class TextPayload : IMutableSePayload
{
    private string? text16;
    private ReadOnlyMemory<byte> text8;

    /// <summary>Initializes a new instance of the <see cref="TextPayload"/> class.</summary>
    public TextPayload()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextPayload"/> class with a given text.</summary>
    /// <param name="value">The text.</param>
    public TextPayload(string? value)
        : this(value.AsSpan())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextPayload"/> class with a given zero-terminated UTF-16
    /// text.</summary>
    /// <param name="value">The zero-terminated text.</param>
    public unsafe TextPayload(char* value)
        : this(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(value))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextPayload"/> class with a given UTF-16 text.</summary>
    /// <param name="value">The text.</param>
    public TextPayload(ReadOnlySpan<char> value)
    {
        EnsureAllowedCharsOrThrow(value);
        this.text16 = value.IsEmpty ? null : new(value);
    }

    /// <summary>Initializes a new instance of the <see cref="TextPayload"/> class with a given zero-terminated UTF-8
    /// text.</summary>
    /// <param name="value">The zero-terminated text.</param>
    public unsafe TextPayload(byte* value)
        : this(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(value))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextPayload"/> class with a given UTF-8 text.</summary>
    /// <param name="value">The text.</param>
    public TextPayload(ReadOnlySpan<byte> value)
    {
        EnsureAllowedCharsOrThrow(value);
        this.text8 = value.IsEmpty ? null : value.ToArray();
    }

    /// <inheritdoc/>
    int IMutableSePayload.MacroCode => 0;

    /// <inheritdoc/>
    int IMutableSePayload.MinExpressionCount => 0;

    /// <inheritdoc/>
    int IMutableSePayload.MaxExpressionCount => 0;

    /// <inheritdoc/>
    IReadOnlyList<IMutableSeExpression> IMutableSePayload.Expressions => Array.Empty<IMutableSeExpression>();

    /// <summary>Gets a value indicating whether this text payload does not contain any text.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(this.text16) && this.text8.IsEmpty;

    /// <summary>Gets or sets the underlying text in UTF-16.</summary>
    public string Text
    {
        get
        {
            if (this.text16 is null && !this.text8.IsEmpty)
                this.text16 = Encoding.UTF8.GetString(this.text8.Span);
            return this.text16 ?? string.Empty;
        }

        set
        {
            if (ReferenceEquals(this.text16, value))
                return;
            EnsureAllowedCharsOrThrow(value.AsSpan());
            this.text16 = value ?? throw new NullReferenceException();
            this.text8 = null;
        }
    }

    /// <summary>Gets or sets the underlying text in UTF-8.</summary>
    public ReadOnlyMemory<byte> Text8
    {
        get
        {
            if (this.text16 is not null && this.text8.IsEmpty)
                this.text8 = Encoding.UTF8.GetBytes(this.text16);
            return this.text8;
        }

        set
        {
            if (value.Equals(this.text8))
                return;
            EnsureAllowedCharsOrThrow(value.Span);
            this.text16 = null;
            this.text8 = value;
        }
    }

    /// <inheritdoc/>
    public void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        if (!this.IsEmpty)
            ssb.Append(this.Text8);
    }

    /// <inheritdoc/>
    public bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var t8 = this.Text8.Span;
        if (span.Length < t8.Length)
            t8[..span.Length].CopyTo(span);
        else
            t8.CopyTo(span);
        bytesWritten = Math.Min(span.Length, t8.Length);
        return bytesWritten == t8.Length;
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation)
    {
        if (this.text8.IsEmpty)
        {
            if (this.text16 is null)
                return 0;

            // 1 codepoint that fits into 1 UTF-16 unit can take up to 3 bytes in UTF-8.
            // If it requires 2, then it can take up to 4 bytes in UTF-8. We overestimate it as 6 bytes.
            if (allowOverestimation)
                return 3 * this.text16.Length;

            this.text8 = Encoding.UTF8.GetBytes(this.text16);
        }

        return this.text8.Length;
    }

    /// <inheritdoc/>
    public byte[] ToBytes() => this.Text8.ToArray();

    /// <inheritdoc/>
    public override string ToString() => this.Text;

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span)
    {
        this.Text8.Span.CopyTo(span);
        return this.Text8.Length;
    }

    /// <inheritdoc/>
    public void WriteToStream(Stream stream) => stream.Write(this.Text8.Span);

    private static void EnsureAllowedCharsOrThrow(ReadOnlySpan<byte> value)
    {
        if (value.ContainsAny((byte)0, (byte)2))
            throw new ArgumentException("NUL and STX are not allowed.", nameof(value));
    }

    private static void EnsureAllowedCharsOrThrow(ReadOnlySpan<char> value)
    {
        if (value.ContainsAny((char)0, (char)2))
            throw new ArgumentException("NUL and STX are not allowed.", nameof(value));
    }
}
