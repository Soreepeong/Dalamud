using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the second value from the contextual time storage.</summary>
public sealed class SecondSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly SecondSeExpression Instance = new();

    private SecondSeExpression()
        : base((byte)ExpressionType.Second)
    {
    }
}
