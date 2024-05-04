using System.Collections.Generic;
using System.IO;

using Dalamud.Game.Text.DSeStringHandling.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Payloads;

/// <summary>Represents an invalid payload.</summary>
public sealed class InvalidSePayload : IMutableSePayload
{
    /// <summary>Initializes a new instance of the <see cref="InvalidSePayload"/> class.</summary>
    public InvalidSePayload()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InvalidSePayload"/> class.</summary>
    /// <param name="data">The initial data.</param>
    public InvalidSePayload(ReadOnlySpan<byte> data) => this.Data = data.ToArray();

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
