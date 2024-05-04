using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;

/// <summary>Evaluates the condition to get the expression among the specified candidates.</summary>
public sealed class SwitchPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="SwitchPayload"/> class.</summary>
    public SwitchPayload()
        : base(1, int.MaxValue, (int)Lumina.Text.Payloads.MacroCode.Switch)
    {
    }

    /// <summary>Gets or sets the condition expression.</summary>
    public IMutableSeExpression? Condition
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the maximum condition value to handle.</summary>
    public int MaxConditionValue
    {
        get => this.Expressions.Count;
        set => this.SetExpressionCount(value);
    }

    /// <summary>Gets the conditional expression for the given value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expression for the given value, or <c>null</c> if not specified.</returns>
    public IMutableSeExpression? GetConditionalExpressionFor(int value) =>
        value < 1 || value >= this.Expressions.Count
            ? null
            : this.ExpressionAt(value);

    /// <summary>Sets the conditional expression for the given value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="expression">The expression.</param>
    public void SetConditionalExpressionFor(int value, IMutableSeExpression? expression)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be at least 1.");
        if (value >= this.Expressions.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Update {nameof(this.MaxConditionValue)} first to handle more values.");
        }

        this.ExpressionAt(value) = expression;
    }
    
    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var e = this.GetConditionalExpressionFor(this.Condition?.EvaluateAsInt(context) ?? 0);
        e?.EvaluateToSeStringBuilder(context, ssb);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var e = this.GetConditionalExpressionFor(this.Condition?.EvaluateAsInt(context) ?? 0);
        if (e is null)
        {
            bytesWritten = 0;
            return true;
        }

        return e.EvaluateToSpan(context, span, out bytesWritten);
    }
}
