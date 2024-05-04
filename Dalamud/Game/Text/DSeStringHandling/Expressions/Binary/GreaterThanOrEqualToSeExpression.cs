using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Binary;

/// <summary>A SeString expression that tests if the first operand is greater than or equal to the second operand.
/// </summary>
public sealed class GreaterThanOrEqualsToSeExpression : BinaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="GreaterThanOrEqualsToSeExpression"/> class.</summary>
    public GreaterThanOrEqualsToSeExpression()
        : base((byte)ExpressionType.GreaterThanOrEqualTo)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GreaterThanOrEqualsToSeExpression"/> class.</summary>
    /// <param name="operand1">The first operand.</param>
    /// <param name="operand2">The second operand.</param>
    public GreaterThanOrEqualsToSeExpression(IMutableSeExpression? operand1, IMutableSeExpression? operand2)
        : base((byte)ExpressionType.GreaterThanOrEqualTo)
    {
        this.Operand1 = operand1;
        this.Operand2 = operand2;
    }
}
