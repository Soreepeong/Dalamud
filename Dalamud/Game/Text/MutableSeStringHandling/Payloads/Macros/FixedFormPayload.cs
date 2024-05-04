using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros;

/// <summary>Base class for payloads with fixed number of payloads.</summary>
public abstract class FixedFormPayload : IMutableSePayload
{
    private readonly List<IMutableSeExpression?> expressions;

    /// <summary>Initializes a new instance of the <see cref="FixedFormPayload"/> class.</summary>
    /// <param name="minExpressionCount">Minimum number of expresssions.</param>
    /// <param name="maxExpressionCount">Maximum number of expresssions.</param>
    /// <param name="macroCode">The macro code.</param>
    protected FixedFormPayload(int minExpressionCount, int maxExpressionCount, int macroCode)
    {
        this.MinExpressionCount = minExpressionCount;
        this.MaxExpressionCount = maxExpressionCount;
        this.MacroCode = macroCode;
        this.expressions = new(maxExpressionCount);
        CollectionsMarshal.SetCount(this.expressions, maxExpressionCount);
    }

    /// <inheritdoc/>
    public int MinExpressionCount { get; }

    /// <inheritdoc/>
    public int MaxExpressionCount { get; }

    /// <inheritdoc/>
    public int MacroCode { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IMutableSeExpression?> Expressions => this.expressions;

    /// <summary>Gets the expressions without the trailing nulls.</summary>
    private ReadOnlySpan<IMutableSeExpression?> EncodedExpressions
    {
        get
        {
            var to = this.expressions.Count - 1;
            while (to >= 0 && this.expressions[to] is null)
                to--;
            return CollectionsMarshal.AsSpan(this.expressions)[..Math.Max(this.MinExpressionCount, to + 1)];
        }
    }

    /// <inheritdoc/>
    public virtual void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        // Default behavior is to copy.
        ssb.AppendDalamud(this);
    }

    /// <inheritdoc/>
    public virtual bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        // Default behavior is to copy.
        var bodyLength = this.CalculateByteCount(false);
        var bodyLengthLength = SeExpressionUtilities.CalculateLengthInt(bodyLength);
        if (span.Length < 3 + bodyLengthLength + bodyLength)
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = this.WriteToSpanCore(span, bodyLength);
        return true;
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation)
    {
        var len = 0;
        foreach (var e in this.EncodedExpressions)
            len += e?.CalculateByteCount(allowOverestimation) ?? 1;
        return 3 + (allowOverestimation ? 5 : SeExpressionUtilities.CalculateLengthInt(len)) + len;
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

    /// <summary>Clears this payload of expressions and sets it from <paramref name="span"/>. </summary>
    /// <param name="span">The payload span.</param>
    /// <returns>This for method chaining.</returns>
    public FixedFormPayload WithExpressionsFromLumina(ReadOnlySePayloadSpan span)
    {
        this.expressions.Clear();
        foreach (var e in span)
            this.expressions.Add(MutableSeExpression.FromLumina(e));
        CollectionsMarshal.SetCount(this.expressions, Math.Max(this.expressions.Count, this.MinExpressionCount));
        return this;
    }

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
        buf[1] = (byte)this.MacroCode;
        SeExpressionUtilities.EncodeInt(new(buf + 2, 5), bodyLength);

        if (stream is MemoryStream ms)
            ms.Capacity = Math.Max(ms.Capacity, checked((int)ms.Length + 3 + bodyLengthLength + bodyLength));

        stream.Write(new(buf, 2 + bodyLengthLength));
        foreach (var e in this.EncodedExpressions)
        {
            if (e is null)
                stream.WriteByte(1);
            else
                e.WriteToStream(stream);
        }

        stream.WriteByte(SeString.EndByte);
    }

    /// <summary>Gets the reference to an expression at the given index.</summary>
    /// <param name="index">Index of the expression.</param>
    /// <returns>Reference to the expression.</returns>
    protected ref IMutableSeExpression? ExpressionAt(int index)
    {
        if (index < 0)
            throw new IndexOutOfRangeException();
        if (this.expressions.Count <= index)
            CollectionsMarshal.SetCount(this.expressions, index + 1);
        return ref CollectionsMarshal.AsSpan(this.expressions)[index];
    }

    /// <summary>Sets the number of expressions.</summary>
    /// <param name="count">Number of expressions.</param>
    protected void SetExpressionCount(int count) =>
        CollectionsMarshal.SetCount(
            this.expressions,
            count >= this.MinExpressionCount
                ? count
                : throw new ArgumentOutOfRangeException(nameof(count), count, null));

    private int WriteToSpanCore(Span<byte> span, int bodyLength)
    {
        var spanBefore = span;
        span = span[SeExpressionUtilities.WriteRaw(span, SeString.StartByte)..];
        span = span[SeExpressionUtilities.WriteRaw(span, (byte)this.MacroCode)..];
        span = span[SeExpressionUtilities.EncodeInt(span, bodyLength)..];
        foreach (var e in this.EncodedExpressions)
            span = e is null ? span[SeExpressionUtilities.WriteRaw(span, 1)..] : span[e.WriteToSpan(span)..];
        span = span[SeExpressionUtilities.WriteRaw(span, SeString.EndByte)..];
        return spanBefore.Length - span.Length;
    }
}
