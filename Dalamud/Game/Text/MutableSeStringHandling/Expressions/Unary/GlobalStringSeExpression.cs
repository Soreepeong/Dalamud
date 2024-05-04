using Lumina.Text;
using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a string in the contextual global value storage.</summary>
public sealed class GlobalStringSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="GlobalStringSeExpression"/> class.</summary>
    public GlobalStringSeExpression()
        : base((byte)ExpressionType.GlobalString)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlobalStringSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public GlobalStringSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.GlobalString) => this.Operand = operand;

    /// <inheritdoc/>
    public override bool EvaluateAsBool(ISeStringEvaluationContext context) =>
        context.GetGlobalString(this.Operand?.EvaluateAsInt(context) ?? 0)?.EvaluateAsBool(context) is true;

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) =>
        context.GetGlobalString(this.Operand?.EvaluateAsInt(context) ?? 0)?.EvaluateAsInt(context) ?? 0;

    /// <inheritdoc/>
    public override uint EvaluateAsUInt(ISeStringEvaluationContext context) =>
        context.GetGlobalString(this.Operand?.EvaluateAsInt(context) ?? 0)?.EvaluateAsUInt(context) ?? 0;

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        ssb.AppendDalamud(context.GetGlobalString(this.Operand?.EvaluateAsInt(context) ?? 0));

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var s = context.GetGlobalString(this.Operand?.EvaluateAsInt(context) ?? 0);
        if (s is null)
        {
            bytesWritten = 0;
            return true;
        }

        return s.EvaluateToSpan(context, span, out bytesWritten);
    }
}
