using System.Collections.Generic;
using System.IO;

namespace Dalamud.Game.Text.DSeStringHandling;

/// <summary>Interface for mutable SeString payloads.</summary>
public interface IMutableSePayload
{
    /// <summary>Gets the minimum number of expressions accepted for this payload.</summary>
    int MinExpressionCount { get; }
    
    /// <summary>Gets the maximum number of expressions accepted for this payload.</summary>
    int MaxExpressionCount { get; }

    /// <summary>Gets the macro code.</summary>
    /// <value><c>0</c> means that the payload is a text payload. Negative values mean that the payload is invalid.
    /// </value>
    int MacroCode { get; }

    /// <summary>Gets the read-only list of expressions that form this payload.</summary>
    IReadOnlyList<IMutableSeExpression> Expressions { get; }

    /// <summary>Calculates the number of bytes required to encode this payload.</summary>
    /// <param name="allowOverestimation">Allow returning a value that may be larger than the exact number of bytes
    /// required, for faster calculation.</param>
    /// <returns>Number of bytes required.</returns>
    int CalculateByteCount(bool allowOverestimation);
    
    /// <summary>Encodes this payload into a byte array.</summary>
    /// <returns>The encoded bytes.</returns>
    byte[] ToBytes();
    
    /// <summary>Encodes this payload into a byte span.</summary>
    /// <param name="span">The span to write this payload to.</param>
    /// <returns>Number of bytes written.</returns>
    /// <remarks>The length of <paramref name="span"/> should be at least <see cref="CalculateByteCount"/>.</remarks>
    public int WriteToSpan(Span<byte> span);
    
    /// <summary>Encodes this payload into a stream.</summary>
    /// <param name="stream">The stream to write this payload to.</param>
    public void WriteToStream(Stream stream);
}
