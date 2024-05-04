using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a number in the contextual global value storage.</summary>
public sealed class GlobalNumberSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="GlobalNumberSeExpression"/> class.</summary>
    public GlobalNumberSeExpression()
        : base((byte)ExpressionType.GlobalNumber)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlobalNumberSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public GlobalNumberSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.GlobalNumber) => this.Operand = operand;
}
