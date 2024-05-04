using System.IO;

using Dalamud.Game.Text.MutableSeStringHandling.Expressions.Unary;

using Lumina.Text;
using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions;

/// <summary>Base class for unary expressions.</summary>
public abstract class UnaryMutableSeExpression : IMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="UnaryMutableSeExpression"/> class.</summary>
    /// <param name="marker">The marker byte.</param>
    protected UnaryMutableSeExpression(byte marker) => this.Marker = marker;

    /// <summary>Gets or sets the operand.</summary>
    /// <value><c>null</c> will be written as a <see cref="IntegerSeExpression"/> containing <c>0</c>.</value>
    public IMutableSeExpression? Operand { get; set; }

    /// <inheritdoc/>
    public string? NativeName => ((ExpressionType)this.Marker).GetNativeName();

    /// <inheritdoc/>
    public byte Marker { get; }

    /// <summary>Creates a new corresponding instance of an unary expression, if applicable.</summary>
    /// <param name="marker">The marker byte.</param>
    /// <param name="operand">The operand.</param>
    /// <returns>The new instance corresponding to the marker, or null if the marker does not represent an unary
    /// expression.</returns>
    public static UnaryMutableSeExpression? From(byte marker, IMutableSeExpression? operand) =>
        (ExpressionType)marker switch
        {
            ExpressionType.LocalNumber => new LocalNumberSeExpression(operand),
            ExpressionType.GlobalNumber => new GlobalNumberSeExpression(operand),
            ExpressionType.LocalString => new LocalStringSeExpression(operand),
            ExpressionType.GlobalString => new GlobalStringSeExpression(operand),
            _ => null,
        };

    /// <inheritdoc/>
    public abstract bool EvaluateAsBool(ISeStringEvaluationContext context);

    /// <inheritdoc/>
    public abstract int EvaluateAsInt(ISeStringEvaluationContext context);

    /// <inheritdoc/>
    public abstract uint EvaluateAsUInt(ISeStringEvaluationContext context);

    /// <inheritdoc/>
    public abstract void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb);

    /// <inheritdoc/>
    public abstract bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten);

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation) =>
        1 + (this.Operand?.CalculateByteCount(allowOverestimation) ?? 1);

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
        span[0] = this.Marker;
        if (this.Operand is null)
        {
            span[1] = 1;
            return 2;
        }

        return 1 + this.Operand.WriteToSpan(span[1..]);
    }

    /// <inheritdoc/>
    public void WriteToStream(Stream stream)
    {
        stream.WriteByte(this.Marker);
        if (this.Operand is null)
            stream.WriteByte(1);
        else
            this.Operand.WriteToStream(stream);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{this.NativeName ?? $"[{this.Marker:X02}]"}({this.Operand?.ToString() ?? "0"})";
}
