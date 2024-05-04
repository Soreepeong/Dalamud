using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Binary;

/// <summary>A SeString expression that tests if the first operand is not equal to the second operand.
/// </summary>
public sealed class NotEqualsToSeExpression : BinaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="NotEqualsToSeExpression"/> class.</summary>
    public NotEqualsToSeExpression()
        : base((byte)ExpressionType.NotEqual)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotEqualsToSeExpression"/> class.</summary>
    /// <param name="operand1">The first operand.</param>
    /// <param name="operand2">The second operand.</param>
    public NotEqualsToSeExpression(IMutableSeExpression? operand1, IMutableSeExpression? operand2)
        : base((byte)ExpressionType.NotEqual)
    {
        this.Operand1 = operand1;
        this.Operand2 = operand2;
    }
}
