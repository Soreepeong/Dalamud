using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Waits for a specified duration before advancing to the next macro line.</summary>
public sealed class WaitPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="WaitPayload"/> class.</summary>
    public WaitPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Wait)
    {
    }

    /// <summary>Gets or sets the wait duration in seconds.</summary>
    public IMutableSeExpression? Seconds
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        base.EvaluateToSeStringBuilder(context, ssb);
        this.UpdateContext(context);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (!base.EvaluateToSpan(context, span, out bytesWritten))
            return false;
        this.UpdateContext(context);
        return true;
    }

    private void UpdateContext(ISeStringEvaluationContext context) =>
        context.WaitTime = TimeSpan.FromSeconds(this.Seconds?.EvaluateAsInt(context) ?? 0);
}
