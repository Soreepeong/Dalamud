using System.Collections.Generic;
using System.IO;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling;

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
    /// <remarks>Non-trailing <c>null</c>s will be encoded as an integer expression containing <c>0</c>.</remarks>
    IReadOnlyList<IMutableSeExpression?> Expressions { get; }

    /// <summary>Evaluates this SeString payload using the given context to a SeString that is not dependent on context.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="ssb">An instance of <see cref="SeStringBuilder"/> to write the evaluation result to.</param>
    void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb);

    /// <summary>Evaluates this SeString payload using the given context to a SeString that is not dependent on context.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="span">The span to write the evaluation result to.</param>
    /// <param name="bytesWritten">Number of bytes written to <paramref name="span"/>.</param>
    /// <returns><c>true</c> if the evaluation result is fully stored in <paramref name="span"/>.</returns>
    bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten);

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
