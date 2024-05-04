using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Binary;

/// <summary>A SeString expression that tests if the first operand is equal to the second operand.
/// </summary>
public sealed class EqualsToSeExpression : BinaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="EqualsToSeExpression"/> class.</summary>
    public EqualsToSeExpression()
        : base((byte)ExpressionType.Equal)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="EqualsToSeExpression"/> class.</summary>
    /// <param name="operand1">The first operand.</param>
    /// <param name="operand2">The second operand.</param>
    public EqualsToSeExpression(IMutableSeExpression? operand1, IMutableSeExpression? operand2)
        : base((byte)ExpressionType.Equal)
    {
        this.Operand1 = operand1;
        this.Operand2 = operand2;
    }
}
