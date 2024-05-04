using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Binary;

/// <summary>A SeString expression that tests if the first operand is less than or equal to the second operand.
/// </summary>
public sealed class LessThanOrEqualToSeExpression : BinaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="LessThanOrEqualToSeExpression"/> class.</summary>
    public LessThanOrEqualToSeExpression()
        : base((byte)ExpressionType.LessThanOrEqualTo)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LessThanOrEqualToSeExpression"/> class.</summary>
    /// <param name="operand1">The first operand.</param>
    /// <param name="operand2">The second operand.</param>
    public LessThanOrEqualToSeExpression(IMutableSeExpression? operand1, IMutableSeExpression? operand2)
        : base((byte)ExpressionType.LessThanOrEqualTo)
    {
        this.Operand1 = operand1;
        this.Operand2 = operand2;
    }
}
