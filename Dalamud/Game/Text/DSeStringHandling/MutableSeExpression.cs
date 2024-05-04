using System.Runtime.InteropServices;

using Dalamud.Game.Text.DSeStringHandling.Expressions;

using Lumina.Text.Expressions;
using Lumina.Text.ReadOnly;

namespace Dalamud.Game.Text.DSeStringHandling;

/// <summary>Utilities for <see cref="IMutableSeExpression"/>.</summary>
public static unsafe class MutableSeExpression
{
    /// <summary>Gets a SeString expression at the given memory address.</summary>
    /// <param name="sz">Pointer to a SeString expression.</param>
    /// <returns>The expression, or <c>null</c> if none was found.</returns>
    public static IMutableSeExpression? From(byte* sz) => From(MemoryMarshal.CreateReadOnlySpanFromNullTerminated(sz));

    /// <summary>Gets the first expression in the given bytes.</summary>
    /// <param name="bytes">Bytes to look for expressions.</param>
    /// <returns>The expression, or <c>null</c> if none was found.</returns>
    public static IMutableSeExpression? From(ReadOnlySpan<byte> bytes) =>
        FromLumina(new ReadOnlySeExpressionSpan(bytes));

    /// <summary>Gets a mutable expression from the given Lumina expression.</summary>
    /// <param name="expression">The expression to convert.</param>
    /// <returns>The converted expression, or <c>null</c> if not supported or applicable.</returns>
    public static IMutableSeExpression? FromLumina(BaseExpression? expression) =>
        expression switch
        {
            IntegerExpression e => new IntegerSeExpression(e.Value),
            PlaceholderExpression e => NullaryMutableSeExpression.From((byte)e.ExpressionType),
            ParameterExpression e => UnaryMutableSeExpression.From((byte)e.ExpressionType, FromLumina(e.Operand)),
            BinaryExpression e => BinaryMutableSeExpression.From(
                (byte)e.ExpressionType,
                FromLumina(e.Operand1),
                FromLumina(e.Operand2)),
            StringExpression e => new StringSeExpression(MutableSeString.FromLumina(e.Value)),
            _ => null,
        };

    /// <summary>Gets a mutable expression from the given Lumina expression.</summary>
    /// <param name="expression">The expression to convert.</param>
    /// <returns>The converted expression, or <c>null</c> if not supported or applicable.</returns>
    public static IMutableSeExpression? FromLumina(ReadOnlySeExpression expression) => FromLumina(expression.AsSpan());

    /// <summary>Gets a mutable expression from the given Lumina expression.</summary>
    /// <param name="expression">The expression to convert.</param>
    /// <returns>The converted expression, or <c>null</c> if not supported or applicable.</returns>
    public static IMutableSeExpression? FromLumina(ReadOnlySeExpressionSpan expression)
    {
        if (expression.TryGetInt(out var intValue))
            return new IntegerSeExpression(intValue);
        if (expression.TryGetPlaceholderExpression(out var exty))
            return NullaryMutableSeExpression.From(exty);
        if (expression.TryGetParameterExpression(out exty, out var operand1))
            return UnaryMutableSeExpression.From(exty, FromLumina(operand1));
        if (expression.TryGetBinaryExpression(out exty, out operand1, out var operand2))
            return BinaryMutableSeExpression.From(exty, FromLumina(operand1), FromLumina(operand2));
        if (expression.TryGetString(out var ss))
            return new StringSeExpression(MutableSeString.FromLumina(ss));
        return null;
    }
}
