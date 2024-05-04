using System.Buffers;
using System.Linq;

using Lumina.Text.Expressions;

namespace Dalamud.Game.Text.MutableSeStringHandling.Expressions.Binary;

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

    /// <inheritdoc/>
    public override unsafe bool EvaluateAsBool(ISeStringEvaluationContext context)
    {
        if (this.Operand1 is null)
        {
            if (this.Operand2 is null)
                return false;
            var dummy = default(uint);
            if (this.Operand2.EvaluateToSpan(context, new(&dummy, sizeof(uint)), out var bytesWritten) is false)
                return true;
            return bytesWritten == 0;
        }

        if (this.Operand2 is null)
        {
            var dummy = default(uint);
            if (this.Operand1.EvaluateToSpan(context, new(&dummy, sizeof(uint)), out var bytesWritten) is false)
                return true;
            return bytesWritten == 0;
        }

        var arr1 = Array.Empty<byte>();
        var arr2 = Array.Empty<byte>();
        for (var len = 4096; len < 0x10000000; len *= 2)
        {
            ArrayPool<byte>.Shared.Return(arr1);
            ArrayPool<byte>.Shared.Return(arr2);

            arr1 = ArrayPool<byte>.Shared.Rent(len);
            arr2 = ArrayPool<byte>.Shared.Rent(len);
            if (!this.Operand1.EvaluateToSpan(context, arr1, out var len1))
                continue;
            if (!this.Operand2.EvaluateToSpan(context, arr2, out var len2))
                continue;

            var cmp = arr1.AsSpan(0, len1).SequenceEqual(arr2.AsSpan(0, len2));
            ArrayPool<byte>.Shared.Return(arr1);
            ArrayPool<byte>.Shared.Return(arr2);
            return !cmp;
        }

        throw new OutOfMemoryException("Temporary evaluation result exceeds 0x10000000 bytes");
    }
}
