using System.IO;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>Interface for mutable SeString expressions.</summary>
public interface IMutableSeExpression
{
    /// <summary>Gets the native name of this expression, if applicable.</summary>
    string? NativeName { get; }

    /// <summary>Gets the marker byte for this expression.</summary>
    byte Marker { get; }

    /// <summary>Evaluates the expression as a boolean value.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated value.</returns>
    bool EvaluateAsBool(ISeStringEvaluationContext context);

    /// <summary>Evaluates the expression as an integer value.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated value.</returns>
    int EvaluateAsInt(ISeStringEvaluationContext context);

    /// <summary>Evaluates the expression as an unsigned integer value.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated value.</returns>
    uint EvaluateAsUInt(ISeStringEvaluationContext context);

    /// <summary>Evaluates the expression to a SeString builder.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="ssb">The target SeString builder.</param>
    void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb);

    /// <summary>Evaluates the expression to a byte span.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="span">The target span.</param>
    /// <param name="bytesWritten">Number of bytes written. Meaningful only if <c>true</c> is returned.</param>
    /// <returns><c>true</c> if evaluated data is fully written to <paramref name="span"/>.</returns>
    bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten);

    /// <summary>Calculates the number of bytes required to encode this expression.</summary>
    /// <param name="allowOverestimation">Allow returning a value that may be larger than the exact number of bytes
    /// required, for faster calculation.</param>
    /// <returns>Number of bytes required.</returns>
    int CalculateByteCount(bool allowOverestimation);

    /// <summary>Encodes this expression into a byte array.</summary>
    /// <returns>The encoded bytes.</returns>
    byte[] ToBytes();

    /// <summary>Encodes this expression into a byte span.</summary>
    /// <param name="span">The span to write this expression to.</param>
    /// <returns>Number of bytes written.</returns>
    /// <remarks>The length of <paramref name="span"/> should be at least <see cref="CalculateByteCount"/>.</remarks>
    int WriteToSpan(Span<byte> span);

    /// <summary>Encodes this expression into a stream.</summary>
    /// <param name="stream">The stream to write this expression to.</param>
    void WriteToStream(Stream stream);
}
