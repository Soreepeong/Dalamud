using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

/// <summary>Represents an expression that fetches the minute value from the contextual time storage.</summary>
public sealed class MinuteSeExpression : NullaryMutableSeExpression
{
    /// <summary>The singleton instance.</summary>
    public static readonly MinuteSeExpression Instance = new();

    private MinuteSeExpression()
        : base((byte)ExpressionType.Minute)
    {
    }

    /// <inheritdoc/>
    public override int EvaluateAsInt(ISeStringEvaluationContext context) => context.ContextualTime.Minute;
}
