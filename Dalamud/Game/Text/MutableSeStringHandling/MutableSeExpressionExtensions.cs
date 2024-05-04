using System.Text;

using Dalamud.Game.Text.MutableSeStringHandling.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>Extension methods associated with <see cref="IMutableSeExpression"/>.</summary>
public static class MutableSeExpressionExtensions
{
    /// <summary>Appends an expression into an instance of <see cref="Lumina.Text.SeStringBuilder"/>.</summary>
    /// <param name="builder">The SeString builder.</param>
    /// <param name="expression">The expression to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static Lumina.Text.SeStringBuilder AppendDalamud(
        this Lumina.Text.SeStringBuilder builder,
        IMutableSeExpression expression)
    {
        switch (expression)
        {
            case IntegerSeExpression e:
                return builder.AppendIntExpression(e.IntValue);
            case NullaryMutableSeExpression e:
                return builder.Append(e.NativeName);
            case UnaryMutableSeExpression e:
                return builder.Append(e.NativeName)
                              .Append('(')
                              .Append(e.Operand)
                              .Append(')');
            case BinaryMutableSeExpression e:
                return builder.Append(e.NativeName)
                              .Append('(')
                              .Append(e.Operand1)
                              .Append(", ")
                              .Append(e.Operand2)
                              .Append(')');
            case StringSeExpression e:
                if (e.Value is null)
                    return builder.Append("\"\"");
                return builder.Append('"')
                              .Append(e.Value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\""))
                              .Append('"');
            default:
                return builder.Append("<? ").Append(expression).Append('>');
        }
    }

    /// <summary>Appends an expression into an instance of <see cref="StringBuilder"/> in an encoded form.</summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="expression">The expression to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static StringBuilder AppendEncoded(
        this StringBuilder builder,
        IMutableSeExpression expression)
    {
        switch (expression)
        {
            case IntegerSeExpression e:
                return builder.Append(e.IntValue);
            case NullaryMutableSeExpression e:
                return builder.Append(e.NativeName);
            case UnaryMutableSeExpression e:
                return builder.Append(e.NativeName)
                              .Append('(')
                              .Append(e.Operand)
                              .Append(')');
            case BinaryMutableSeExpression e:
                return builder.Append(e.NativeName)
                              .Append('(')
                              .Append(e.Operand1)
                              .Append(", ")
                              .Append(e.Operand2)
                              .Append(')');
            case StringSeExpression e:
                if (e.Value is null)
                    return builder.Append("\"\"");
                return builder.Append('"')
                              .Append(e.Value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\""))
                              .Append('"');
            default:
                return builder.Append("<? ").Append(expression).Append('>');
        }
    }
}
