using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the day value from the contextual time storage.</summary>
public sealed class DaySeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly DaySeExpression Instance = new();

    private DaySeExpression()
        : base((byte)ExpressionType.Day)
    {
    }

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) => context.ContextualTime.Day;
}
