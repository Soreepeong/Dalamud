using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a number in the contextual local value storage.</summary>
public sealed class LocalNumberSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="LocalNumberSeExpression"/> class.</summary>
    public LocalNumberSeExpression()
        : base((byte)ExpressionType.LocalNumber)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalNumberSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public LocalNumberSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.LocalNumber) => this.Operand = operand;
}
