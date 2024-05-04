using System.IO;

using Dalamud.Game.Text.MutableSeStringHandling.Expressions.Binary;

using Lumina.Text;
using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions;

/// <summary>Base class for unary expressions.</summary>
public abstract class BinaryMutableSeExpression : IMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="BinaryMutableSeExpression"/> class.</summary>
    /// <param name="marker">The marker byte.</param>
    protected BinaryMutableSeExpression(byte marker) => this.Marker = marker;

    /// <summary>Gets or sets the first operand.</summary>
    /// <value><c>null</c> will be written as a <see cref="IntegerSeExpression"/> containing <c>0</c>.</value>
    public IMutableSeExpression? Operand1 { get; set; }

    /// <summary>Gets or sets the second operand.</summary>
    /// <value><c>null</c> will be written as a <see cref="IntegerSeExpression"/> containing <c>0</c>.</value>
    public IMutableSeExpression? Operand2 { get; set; }

    /// <inheritdoc/>
    public string? NativeName => ((ExpressionType)this.Marker).GetNativeName();

    /// <inheritdoc/>
    public byte Marker { get; }

    /// <summary>Creates a new corresponding instance of a binary expression, if applicable.</summary>
    /// <param name="marker">The marker byte.</param>
    /// <param name="operand1">The first operand.</param>
    /// <param name="operand2">The second operand.</param>
    /// <returns>The new instance corresponding to the marker, or null if the marker does not represent a binary
    /// expression.</returns>
    public static BinaryMutableSeExpression? From(
        byte marker,
        IMutableSeExpression? operand1,
        IMutableSeExpression? operand2) =>
        (ExpressionType)marker switch
        {
            ExpressionType.GreaterThanOrEqualTo => new GreaterThanOrEqualsToSeExpression(operand1, operand2),
            ExpressionType.GreaterThan => new GreaterThanSeExpression(operand1, operand2),
            ExpressionType.LessThanOrEqualTo => new LessThanOrEqualToSeExpression(operand1, operand2),
            ExpressionType.LessThan => new LessThanSeExpression(operand1, operand2),
            ExpressionType.Equal => new EqualsToSeExpression(operand1, operand2),
            ExpressionType.NotEqual => new NotEqualsToSeExpression(operand1, operand2),
            _ => null,
        };

    /// <inheritdoc/>
    public abstract bool EvaluateAsBool(ISeStringEvaluationContext context);

    /// <inheritdoc/>
    public int EvaluateAsInt(ISeStringEvaluationContext context) => this.EvaluateAsBool(context) ? 1 : 0;

    /// <inheritdoc/>
    public uint EvaluateAsUInt(ISeStringEvaluationContext context) => this.EvaluateAsBool(context) ? 1u : 0u;

    /// <inheritdoc/>
    public void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        ssb.Append(this.EvaluateAsBool(context) ? "1"u8 : "0"u8);

    /// <inheritdoc/>
    public bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (span.IsEmpty)
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = 1;
        span[0] = this.EvaluateAsBool(context) ? (byte)'1' : (byte)'0';
        return true;
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation) =>
        1
        + (this.Operand1?.CalculateByteCount(allowOverestimation) ?? 1)
        + (this.Operand2?.CalculateByteCount(allowOverestimation) ?? 1);

    /// <inheritdoc/>
    public byte[] ToBytes()
    {
        var res = new byte[this.CalculateByteCount(false)];
        this.WriteToSpan(res);
        return res;
    }

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span)
    {
        var ptr = 0;
        span[ptr++] = this.Marker;

        if (this.Operand1 is null)
            span[ptr++] = 1;
        else
            ptr += this.Operand1.WriteToSpan(span[ptr..]);

        if (this.Operand2 is null)
            span[ptr++] = 1;
        else
            ptr += this.Operand2.WriteToSpan(span[ptr..]);

        return ptr;
    }

    /// <inheritdoc/>
    public void WriteToStream(Stream stream)
    {
        stream.WriteByte(this.Marker);
        if (this.Operand1 is null)
            stream.WriteByte(1);
        else
            this.Operand1.WriteToStream(stream);
        if (this.Operand2 is null)
            stream.WriteByte(1);
        else
            this.Operand2.WriteToStream(stream);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{this.NativeName ?? $"[{this.Marker:X02}]"}({this.Operand1?.ToString() ?? "0"}, {this.Operand2?.ToString() ?? "0"})";
}
