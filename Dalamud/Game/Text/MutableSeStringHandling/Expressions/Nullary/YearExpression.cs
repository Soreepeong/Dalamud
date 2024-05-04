using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the year value from the contextual time storage.</summary>
public sealed class YearSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly YearSeExpression Instance = new();

    private YearSeExpression()
        : base((byte)ExpressionType.Year)
    {
    }

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) => context.ContextualTime.Year;
}
