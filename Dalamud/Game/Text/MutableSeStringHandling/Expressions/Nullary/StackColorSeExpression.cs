using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that restores the color stack associated with the payload type.</summary>
public sealed class StackColorSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly StackColorSeExpression Instance = new();

    private StackColorSeExpression()
        : base((byte)ExpressionType.StackColor)
    {
    }

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) =>
        context.TryPeekColor(out var color) ? unchecked((int)color) : 0;
}
