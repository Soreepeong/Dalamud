using Lumina.Text;
using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a string in the contextual local value storage.</summary>
public sealed class LocalStringSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="LocalStringSeExpression"/> class.</summary>
    public LocalStringSeExpression()
        : base((byte)ExpressionType.LocalString)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalStringSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public LocalStringSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.LocalString) => this.Operand = operand;

    /// <inheritdoc/>
    public override bool EvaluateAsBool(ISeStringEvaluationContext context) =>
        context.GetLocalString(this.Operand?.EvaluateAsInt(context) ?? 0)?.EvaluateAsBool(context) is true;

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) =>
        context.GetLocalString(this.Operand?.EvaluateAsInt(context) ?? 0)?.EvaluateAsInt(context) ?? 0;

    /// <inheritdoc/>
    public override uint EvaluateAsUInt(ISeStringEvaluationContext context) =>
        context.GetLocalString(this.Operand?.EvaluateAsInt(context) ?? 0)?.EvaluateAsUInt(context) ?? 0;

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        ssb.AppendDalamud(context.GetLocalString(this.Operand?.EvaluateAsInt(context) ?? 0));

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var s = context.GetLocalString(this.Operand?.EvaluateAsInt(context) ?? 0);
        if (s is null)
        {
            bytesWritten = 0;
            return true;
        }

        return s.EvaluateToSpan(context, span, out bytesWritten);
    }
}
