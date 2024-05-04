using Dalamud.Game.Text.MutableSeStringHandling.Expressions;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets the contextual time storage value to the calculated reset time.</summary>
public sealed class SetTimePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="SetTimePayload"/> class.</summary>
    public SetTimePayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.SetTime)
    {
    }

    /// <summary>Gets or sets the time expression in unix epoch in seconds unit.</summary>
    public IMutableSeExpression? Time
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Sets <see cref="Time"/> with the given integer value.</summary>
    /// <param name="timeValue">The integer value.</param>
    public void SetTime(int timeValue) => this.Time = new IntegerSeExpression(timeValue);

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        this.UpdateContext(context);

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        this.UpdateContext(context);
        bytesWritten = 0;
        return true;
    }

    private void UpdateContext(ISeStringEvaluationContext context) =>
        context.ContextualTime =
            DateTimeOffset.FromUnixTimeSeconds(this.Time?.EvaluateAsInt(context) ?? 0).LocalDateTime;
}
