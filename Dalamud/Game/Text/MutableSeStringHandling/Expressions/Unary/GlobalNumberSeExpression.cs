using System.Numerics;

using Lumina.Text;
using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a number in the contextual global value storage.</summary>
public sealed class GlobalNumberSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="GlobalNumberSeExpression"/> class.</summary>
    public GlobalNumberSeExpression()
        : base((byte)ExpressionType.GlobalNumber)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlobalNumberSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public GlobalNumberSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.GlobalNumber) => this.Operand = operand;

    /// <inheritdoc/>
    public override bool EvaluateAsBool(ISeStringEvaluationContext context) =>
        context.GetGlobalNumber(this.Operand?.EvaluateAsInt(context) ?? 0) != 0;

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) =>
        context.GetGlobalNumber(this.Operand?.EvaluateAsInt(context) ?? 0);

    /// <inheritdoc/>
    public override uint EvaluateAsUInt(ISeStringEvaluationContext context) =>
        unchecked((uint)this.EvaluateAsInt(context));

    /// <inheritdoc/>
    public override unsafe void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var bufStorage = default(Vector4);
        var buf = new Span<byte>(&bufStorage, sizeof(Vector4));
        if (!this.EvaluateToSpan(context, buf, out var len))
            throw new InvalidOperationException("int.MinValue.ToString().Length should have fit into 16 bytes");
        ssb.Append(buf[..len]);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten) =>
        this.EvaluateAsInt(context).TryFormat(span, out bytesWritten);
}
