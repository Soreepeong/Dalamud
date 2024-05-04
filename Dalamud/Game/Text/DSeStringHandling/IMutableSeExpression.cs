using System.IO;

namespace Dalamud.Game.Text.DSeStringHandling;

/// <summary>Interface for mutable SeString expressions.</summary>
public interface IMutableSeExpression
{
    /// <summary>Gets the native name of this expression, if applicable.</summary>
    string? NativeName { get; }

    /// <summary>Gets the marker byte for this expression.</summary>
    public byte Marker { get; }

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
    public int WriteToSpan(Span<byte> span);

    /// <summary>Encodes this expression into a stream.</summary>
    /// <param name="stream">The stream to write this expression to.</param>
    public void WriteToStream(Stream stream);
}
