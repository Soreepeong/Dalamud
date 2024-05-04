using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a string in the contextual global value storage.</summary>
public sealed class GlobalStringSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="GlobalStringSeExpression"/> class.</summary>
    public GlobalStringSeExpression()
        : base((byte)ExpressionType.GlobalString)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GlobalStringSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public GlobalStringSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.GlobalString) => this.Operand = operand;
}
