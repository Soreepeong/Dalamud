using System.Collections.Generic;
using System.IO;

using Dalamud.Game.Text.DSeStringHandling.Expressions;

using Lumina.Text;

namespace Dalamud.Game.Text.DSeStringHandling.Payloads.Macros;

/// <summary>A payload that may contain any number of expressions.</summary>
public sealed class FreeformSePayload : List<IMutableSeExpression>, IMutableSePayload
{
    private int macroCode = 0xFF;

    /// <summary>Initializes a new instance of the <see cref="FreeformSePayload"/> class.</summary>
    public FreeformSePayload()
    {
        // you have acquired a taste for freeform jazz
    }

    /// <summary>Initializes a new instance of the <see cref="FreeformSePayload"/> class.</summary>
    /// <param name="macroCode">The macro code.</param>
    public FreeformSePayload(int macroCode) => this.macroCode = macroCode;

    /// <inheritdoc/>
    public int MacroCode
    {
        get => this.macroCode;
        set => this.macroCode =
                   value is < 1 or > 0xFF ? throw new ArgumentOutOfRangeException(nameof(value), value, null) : value;
    }

    /// <inheritdoc/>
    public int MinExpressionCount => 0;

    /// <inheritdoc/>
    public int MaxExpressionCount => int.MaxValue;

    /// <inheritdoc/>
    public IReadOnlyList<IMutableSeExpression> Expressions => this;

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation)
    {
        var len = 0;
        foreach (var e in this)
            len += e.CalculateByteCount(allowOverestimation);
        len += 3 + (allowOverestimation ? 5 : SeExpressionUtilities.CalculateLengthInt(len));
        return len;
    }

    /// <inheritdoc/>
    public byte[] ToBytes()
    {
        var bodyLength = this.CalculateByteCount(false);
        var bodyLengthLength = SeExpressionUtilities.CalculateLengthInt(bodyLength);
        var res = new byte[3 + bodyLengthLength + bodyLength];
        this.WriteToSpanCore(res, bodyLength);
        return res;
    }

    /// <inheritdoc/>
    public override string ToString() => string.Empty;

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span) => this.WriteToSpanCore(span, this.CalculateByteCount(false));

    /// <inheritdoc/>
    public unsafe void WriteToStream(Stream stream)
    {
        var bodyLength = this.CalculateByteCount(false);
        var bodyLengthLength = SeExpressionUtilities.CalculateLengthInt(bodyLength);

        var bufStorage = default(ulong);
        var buf = (byte*)&bufStorage;
        buf[0] = SeString.StartByte;
        buf[1] = (byte)this.macroCode;
        SeExpressionUtilities.EncodeInt(new(buf + 2, 5), bodyLength);

        if (stream is MemoryStream ms)
            ms.Capacity = Math.Max(ms.Capacity, checked((int)ms.Length + 3 + bodyLengthLength + bodyLength));

        stream.Write(new(buf, 2 + bodyLengthLength));
        foreach (var e in this)
            e.WriteToStream(stream);
        stream.WriteByte(SeString.EndByte);
    }

    private int WriteToSpanCore(Span<byte> span, int bodyLength)
    {
        var spanBefore = span;
        span = span[SeExpressionUtilities.WriteRaw(span, SeString.StartByte)..];
        span = span[SeExpressionUtilities.WriteRaw(span, (byte)this.macroCode)..];
        span = span[SeExpressionUtilities.EncodeInt(span, bodyLength)..];
        foreach (var s in this)
            span = span[s.WriteToSpan(span)..];
        span = span[SeExpressionUtilities.WriteRaw(span, SeString.EndByte)..];
        return spanBefore.Length - span.Length;
    }
}
