using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the weekday value from the contextual time storage.</summary>
public sealed class WeekdaySeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly WeekdaySeExpression Instance = new();

    private WeekdaySeExpression()
        : base((byte)ExpressionType.Weekday)
    {
    }
}
