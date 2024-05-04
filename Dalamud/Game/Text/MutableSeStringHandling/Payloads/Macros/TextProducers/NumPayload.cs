using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a decimal text representation of an integer.</summary>
public sealed class NumPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="NumPayload"/> class.</summary>
    public NumPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Num)
    {
    }

    /// <summary>Gets or sets the integer expression.</summary>
    public IMutableSeExpression? Value
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        ssb.Append(this.Value?.EvaluateAsInt(context) ?? 0);

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten) =>
        (this.Value?.EvaluateAsInt(context) ?? 0).TryFormat(span, out bytesWritten);
}
