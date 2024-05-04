using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a text.</summary>
public sealed class StringPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="StringPayload"/> class.</summary>
    public StringPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.String)
    {
    }

    /// <summary>Gets or sets the string expression.</summary>
    public IMutableSeExpression? Value
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        this.Value?.EvaluateToSeStringBuilder(context, ssb);

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (this.Value is null)
        {
            bytesWritten = 0;
            return true;
        }

        return this.Value.EvaluateToSpan(context, span, out bytesWritten);
    }
}
