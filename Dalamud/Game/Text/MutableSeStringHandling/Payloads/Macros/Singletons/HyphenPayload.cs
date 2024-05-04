using System.Collections.Generic;
using System.IO;

using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Singletons;

/// <summary>A payload that inserts a hyphen.</summary>
public sealed class HyphenPayload : IMutableSePayload
{
    /// <summary>The singleton instance of <see cref="HyphenPayload"/>.</summary>
    public static readonly HyphenPayload Instance = new();

    private HyphenPayload()
    {
    }

    /// <summary>Gets the immutable bytes for representing this payload.</summary>
    public static ReadOnlySpan<byte> Bytes => "\x02\x1F\x01\x03"u8;

    /// <inheritdoc/>
    public int MinExpressionCount => 0;

    /// <inheritdoc/>
    public int MaxExpressionCount => 0;

    /// <inheritdoc/>
    public int MacroCode => (int)Lumina.Text.Payloads.MacroCode.Hyphen;

    /// <inheritdoc/>
    public IReadOnlyList<IMutableSeExpression> Expressions => Array.Empty<IMutableSeExpression>();

    /// <inheritdoc/>
    public void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        ssb.Append(new ReadOnlySeStringSpan(Bytes));

    /// <inheritdoc/>
    public bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (span.Length < Bytes.Length)
            Bytes[..span.Length].CopyTo(span);
        else
            Bytes.CopyTo(span);
        bytesWritten = Math.Min(span.Length, Bytes.Length);
        return bytesWritten == Bytes.Length;
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation) => Bytes.Length;

    /// <inheritdoc/>
    public byte[] ToBytes() => Bytes.ToArray();

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span)
    {
        Bytes.CopyTo(span);
        return Bytes.Length;
    }

    /// <inheritdoc/>
    public void WriteToStream(Stream stream) => stream.Write(Bytes);
}
