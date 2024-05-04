using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;

/// <summary>Tests if the object is the local player to get the corresponding expression.</summary>
public sealed class IfSelfPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="IfSelfPayload"/> class.</summary>
    public IfSelfPayload()
        : base(3, 3, (int)Lumina.Text.Payloads.MacroCode.IfSelf)
    {
    }

    /// <summary>Gets or sets the expression that evaluates to the target object ID.</summary>
    public IMutableSeExpression? ObjectId
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="ObjectId"/> evaluates to the local player.</summary>
    public IMutableSeExpression? TrueExpression
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="ObjectId"/> does not evaluate to the local player.
    /// </summary>
    public IMutableSeExpression? FalseExpression
    {
        get => this.ExpressionAt(2);
        set => this.ExpressionAt(2) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var e = (this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000)) == context.GetLocalPlayerId()
                    ? this.TrueExpression
                    : this.FalseExpression;
        e?.EvaluateToSeStringBuilder(context, ssb);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var e = (this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000)) == context.GetLocalPlayerId()
                    ? this.TrueExpression
                    : this.FalseExpression;
        if (e is null)
        {
            bytesWritten = 0;
            return true;
        }

        return e.EvaluateToSpan(context, span, out bytesWritten);
    }
}
