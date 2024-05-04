using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions.Unary;

/// <summary>A SeString expression that evaluates to a string in the contextual local value storage.</summary>
public sealed class LocalStringSeExpression : UnaryMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="LocalStringSeExpression"/> class.</summary>
    public LocalStringSeExpression()
        : base((byte)ExpressionType.LocalString)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LocalStringSeExpression"/> class.</summary>
    /// <param name="operand">The initial operand.</param>
    public LocalStringSeExpression(IMutableSeExpression? operand)
        : base((byte)ExpressionType.LocalString) => this.Operand = operand;
}
