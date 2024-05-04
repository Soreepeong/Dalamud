using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;

/// <summary>Sets the contextual time storage value to the calculated reset time.</summary>
public sealed class IfPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="IfPayload"/> class.</summary>
    public IfPayload()
        : base(3, 3, (int)Lumina.Text.Payloads.MacroCode.If)
    {
    }

    /// <summary>Gets or sets the condition expression.</summary>
    public IMutableSeExpression? Condition
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="Condition"/> evaluates to <c>true</c>.</summary>
    public IMutableSeExpression? TrueExpression
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="Condition"/> evaluates to <c>false</c>.</summary>
    public IMutableSeExpression? FalseExpression
    {
        get => this.ExpressionAt(2);
        set => this.ExpressionAt(2) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var e = this.Condition?.EvaluateAsBool(context) is true ? this.TrueExpression : this.FalseExpression;
        e?.EvaluateToSeStringBuilder(context, ssb);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var e = this.Condition?.EvaluateAsBool(context) is true ? this.TrueExpression : this.FalseExpression;
        if (e is null)
        {
            bytesWritten = 0;
            return true;
        }

        return e.EvaluateToSpan(context, span, out bytesWritten);
    }
}
