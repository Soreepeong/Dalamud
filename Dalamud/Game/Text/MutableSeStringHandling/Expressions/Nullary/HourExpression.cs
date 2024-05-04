using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the hour value from the contextual time storage.</summary>
public sealed class HourSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly HourSeExpression Instance = new();

    private HourSeExpression()
        : base((byte)ExpressionType.Hour)
    {
    }

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) => context.ContextualTime.Hour;
}
