using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;

/// <summary>Tests the object gender to a given string expression to get the corresponding expression.</summary>
public sealed class IfPcGenderPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="IfPcGenderPayload"/> class.</summary>
    public IfPcGenderPayload()
        : base(3, 3, (int)Lumina.Text.Payloads.MacroCode.IfPcGender)
    {
    }

    /// <summary>Gets or sets the expression that evaluates to the target object ID.</summary>
    public IMutableSeExpression? ObjectId
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="ObjectId"/> evaluates to a male.</summary>
    public IMutableSeExpression? MaleExpression
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="ObjectId"/> evaluates to a female.</summary>
    public IMutableSeExpression? FemaleExpression
    {
        get => this.ExpressionAt(2);
        set => this.ExpressionAt(2) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var e = context.IsObjectMale(this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000))
                    ? this.MaleExpression
                    : this.FemaleExpression;
        e?.EvaluateToSeStringBuilder(context, ssb);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var e = context.IsObjectMale(this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000))
                    ? this.MaleExpression
                    : this.FemaleExpression;
        if (e is null)
        {
            bytesWritten = 0;
            return true;
        }

        return e.EvaluateToSpan(context, span, out bytesWritten);
    }
}
