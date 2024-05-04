using System.IO;
using System.Numerics;

using Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

using Lumina.Text;
using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions;

/// <summary>Base class for nullary expressions.</summary>
public abstract class NullaryMutableSeExpression : IMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="NullaryMutableSeExpression"/> class.</summary>
    /// <param name="marker">The marker byte.</param>
    protected NullaryMutableSeExpression(byte marker) => this.Marker = marker;

    /// <inheritdoc/>
    public string? NativeName => ((ExpressionType)this.Marker).GetNativeName();

    /// <inheritdoc/>
    public byte Marker { get; }

    /// <summary>Gets the singleton instance of a nullary expression, if applicable.</summary>
    /// <param name="marker">The marker byte.</param>
    /// <returns>The singleton instance corresponding to the marker, or null if the marker does not represent a
    /// nullary expression.</returns>
    public static NullaryMutableSeExpression? From(byte marker) => (ExpressionType)marker switch
    {
        ExpressionType.Millisecond => MillisecondSeExpression.Instance,
        ExpressionType.Second => SecondSeExpression.Instance,
        ExpressionType.Minute => MinuteSeExpression.Instance,
        ExpressionType.Hour => HourSeExpression.Instance,
        ExpressionType.Day => DaySeExpression.Instance,
        ExpressionType.Weekday => WeekdaySeExpression.Instance,
        ExpressionType.Month => MonthSeExpression.Instance,
        ExpressionType.Year => YearSeExpression.Instance,
        ExpressionType.StackColor => StackColorSeExpression.Instance,
        _ => null,
    };

    /// <inheritdoc/>
    public bool EvaluateAsBool(ISeStringEvaluationContext context) => this.EvaluateAsInt(context) != 0;

    /// <inheritdoc/>
    public abstract int EvaluateAsInt(ISeStringEvaluationContext context);

    /// <inheritdoc/>
    public uint EvaluateAsUInt(ISeStringEvaluationContext context) => unchecked((uint)this.EvaluateAsInt(context));

    /// <inheritdoc/>
    public unsafe void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var bufStorage = default(Vector4);
        var buf = new Span<byte>(&bufStorage, sizeof(Vector4));
        if (!this.EvaluateToSpan(context, buf, out var len))
            throw new InvalidOperationException("int.MinValue.ToString().Length should have fit into 16 bytes");
        ssb.Append(buf[..len]);
    }

    /// <inheritdoc/>
    public bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten) =>
        this.EvaluateAsInt(context).TryFormat(span, out bytesWritten);

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation) => 1;

    /// <inheritdoc/>
    public byte[] ToBytes() => [this.Marker];

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span)
    {
        if (span.IsEmpty)
            return 0;
        span[0] = this.Marker;
        return 1;
    }

    /// <inheritdoc/>
    public void WriteToStream(Stream stream) => stream.WriteByte(this.Marker);

    /// <inheritdoc/>
    public override string ToString() => this.NativeName ?? $"[{this.Marker:X02}]";
}
