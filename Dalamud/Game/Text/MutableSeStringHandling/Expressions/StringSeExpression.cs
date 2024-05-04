using System.IO;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions;

/// <summary>A SeString expression that contains a SeString.</summary>
public sealed class StringSeExpression : IMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="StringSeExpression"/> class.</summary>
    public StringSeExpression()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StringSeExpression"/> class.</summary>
    /// <param name="value">The initial value.</param>
    public StringSeExpression(MutableSeString? value) => this.Value = value;

    /// <summary>Gets or sets the contained SeString.</summary>
    public MutableSeString? Value { get; set; }

    /// <inheritdoc/>
    string? IMutableSeExpression.NativeName => null;

    /// <inheritdoc/>
    public byte Marker => 0xFF;

    /// <inheritdoc/>
    public bool EvaluateAsBool(ISeStringEvaluationContext context) => this.Value?.EvaluateAsBool(context) is true;

    /// <inheritdoc/>
    public int EvaluateAsInt(ISeStringEvaluationContext context) => this.Value?.EvaluateAsInt(context) ?? 0;

    /// <inheritdoc/>
    public uint EvaluateAsUInt(ISeStringEvaluationContext context) => this.Value?.EvaluateAsUInt(context) ?? 0;

    /// <inheritdoc/>
    public void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        this.Value?.EvaluateToSeStringBuilder(context, ssb);

    /// <inheritdoc/>
    public bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (this.Value is null)
        {
            bytesWritten = 0;
            return true;
        }

        return this.Value.EvaluateToSpan(context, span, out bytesWritten);
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation)
    {
        if (this.Value is null)
            return 1;

        if (allowOverestimation)
            return 6 + this.Value.CalculateByteCount(true);

        var len = this.Value.CalculateByteCount(false);
        return 1 + SeExpressionUtilities.CalculateLengthInt(len) + len;
    }

    /// <inheritdoc/>
    public byte[] ToBytes()
    {
        if (this.Value is null)
            return [0xFF, 0x01];

        var valueLength = this.Value.CalculateByteCount(false);
        var buf = new byte[1 + SeExpressionUtilities.CalculateLengthInt(valueLength) + valueLength];
        this.WriteToSpanCore(buf, valueLength);
        return buf;
    }

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span) =>
        this.WriteToSpanCore(span, this.Value?.CalculateByteCount(false) ?? 0);

    /// <inheritdoc/>
    public unsafe void WriteToStream(Stream stream)
    {
        if (this.Value is null)
        {
            stream.Write([0xFF, 0x01]);
            return;
        }

        var valueLength = this.Value.CalculateByteCount(false);
        var valueLengthLength = SeExpressionUtilities.CalculateLengthInt(valueLength);

        var bufStorage = default(ulong);
        var buf = (byte*)&bufStorage;
        buf[0] = 0xFF;
        SeExpressionUtilities.EncodeInt(new(buf + 1, 5), valueLength);

        if (stream is MemoryStream ms)
            ms.Capacity = Math.Max(ms.Capacity, checked((int)ms.Length + 3 + valueLengthLength + valueLength));

        stream.Write(new(buf, 1 + valueLengthLength));
        this.Value.WriteToStream(stream);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        (this.Value?.ToString() ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("<", "\\<");

    private int WriteToSpanCore(Span<byte> span, int valueLength)
    {
        var spanBefore = span;
        span = span[SeExpressionUtilities.WriteRaw(span, 0xFF)..];
        span = span[SeExpressionUtilities.EncodeInt(span, valueLength)..];
        span = span[(this.Value?.WriteToSpan(span) ?? 0)..];
        return spanBefore.Length - span.Length;
    }
}
