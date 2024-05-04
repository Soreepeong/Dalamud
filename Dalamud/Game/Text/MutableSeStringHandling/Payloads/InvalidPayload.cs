using System.Collections.Generic;
using System.IO;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads;

/// <summary>Represents an invalid payload.</summary>
public sealed class InvalidPayload : IMutableSePayload
{
    /// <summary>Initializes a new instance of the <see cref="InvalidPayload"/> class.</summary>
    public InvalidPayload()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidPayload"/> class.</summary>
    /// <param name="data">The initial data.</param>
    public InvalidPayload(ReadOnlySpan<byte> data) => this.Data = data.ToArray();

    /// <summary>Gets or sets the raw data behind this invalid payload.</summary>
    public ReadOnlyMemory<byte> Data { get; set; }

    /// <inheritdoc/>
    int IMutableSePayload.MinExpressionCount => 0;

    /// <inheritdoc/>
    int IMutableSePayload.MaxExpressionCount => 0;

    /// <inheritdoc/>
    int IMutableSePayload.MacroCode => -1;

    /// <inheritdoc/>
    IReadOnlyList<IMutableSeExpression> IMutableSePayload.Expressions => Array.Empty<IMutableSeExpression>();

    /// <inheritdoc/>
    public void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        ssb.Append(this.Data);

    /// <inheritdoc/>
    bool IMutableSePayload.EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var t8 = this.Data.Span;
        if (span.Length < t8.Length)
            t8[..span.Length].CopyTo(span);
        else
            t8.CopyTo(span);
        bytesWritten = Math.Min(span.Length, t8.Length);
        return bytesWritten == t8.Length;
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation) => this.Data.Length;

    /// <inheritdoc/>
    public byte[] ToBytes() => this.Data.ToArray();

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span)
    {
        this.Data.Span.CopyTo(span);
        return this.Data.Length;
    }

    /// <inheritdoc/>
    public void WriteToStream(Stream stream) => stream.Write(this.Data.Span);
}
