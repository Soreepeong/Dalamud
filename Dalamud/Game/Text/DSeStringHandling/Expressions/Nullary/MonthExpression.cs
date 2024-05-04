using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the month value from the contextual time storage.</summary>
public sealed class MonthSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly MonthSeExpression Instance = new();

    private MonthSeExpression()
        : base((byte)ExpressionType.Month)
    {
    }
}
