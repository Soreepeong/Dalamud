using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the millisecond value from the contextual time storage.</summary>
public sealed class MillisecondSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly MillisecondSeExpression Instance = new();

    private MillisecondSeExpression()
        : base((byte)ExpressionType.Millisecond)
    {
    }
}
