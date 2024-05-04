using System.Buffers;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;

/// <summary>Tests the object name to a given string expression to get the corresponding expression.</summary>
public sealed class IfPcNamePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="IfPcNamePayload"/> class.</summary>
    public IfPcNamePayload()
        : base(4, 4, (int)Lumina.Text.Payloads.MacroCode.IfPcName)
    {
    }

    /// <summary>Gets or sets the expression that evaluates to the target object ID.</summary>
    public IMutableSeExpression? ObjectId
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the name to test against.</summary>
    public IMutableSeExpression? TestName
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="ObjectId"/> evaluates to a male.</summary>
    public IMutableSeExpression? MaleExpression
    {
        get => this.ExpressionAt(2);
        set => this.ExpressionAt(2) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="ObjectId"/> evaluates to a female.</summary>
    public IMutableSeExpression? FemaleExpression
    {
        get => this.ExpressionAt(3);
        set => this.ExpressionAt(3) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var eName = context.GetObjectName(this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000));
        if (eName is null && this.TestName is null)
            return;
        
        var arr1 = Array.Empty<byte>();
        var arr2 = Array.Empty<byte>();
        for (var len = 4096; len < 0x10000000; len *= 2)
        {
            ArrayPool<byte>.Shared.Return(arr1);
            ArrayPool<byte>.Shared.Return(arr2);

            arr1 = ArrayPool<byte>.Shared.Rent(len);
            arr2 = ArrayPool<byte>.Shared.Rent(len);
            var len1 = 0;
            var len2 = 0;
            if (eName?.EvaluateToSpan(context, arr1, out len1) is false)
                continue;
            if (this.TestName?.EvaluateToSpan(context, arr2, out len2) is false)
                continue;

            var cmp = arr1.AsSpan(0, len1).SequenceEqual(arr2.AsSpan(0, len2));
            ArrayPool<byte>.Shared.Return(arr1);
            ArrayPool<byte>.Shared.Return(arr2);

            var e = cmp ? this.MaleExpression : this.FemaleExpression;
            e?.EvaluateToSeStringBuilder(context, ssb);
            return;
        }

        throw new OutOfMemoryException("Temporary evaluation result exceeds 0x10000000 bytes");
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var eName = context.GetObjectName(this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000));
        if (eName is null && this.TestName is null)
        {
            bytesWritten = 0;
            return true;
        }
        
        var arr1 = Array.Empty<byte>();
        var arr2 = Array.Empty<byte>();
        for (var len = 4096; len < 0x10000000; len *= 2)
        {
            ArrayPool<byte>.Shared.Return(arr1);
            ArrayPool<byte>.Shared.Return(arr2);

            arr1 = ArrayPool<byte>.Shared.Rent(len);
            arr2 = ArrayPool<byte>.Shared.Rent(len);
            var len1 = 0;
            var len2 = 0;
            if (eName?.EvaluateToSpan(context, arr1, out len1) is false)
                continue;
            if (this.TestName?.EvaluateToSpan(context, arr2, out len2) is false)
                continue;

            var cmp = arr1.AsSpan(0, len1).SequenceEqual(arr2.AsSpan(0, len2));
            ArrayPool<byte>.Shared.Return(arr1);
            ArrayPool<byte>.Shared.Return(arr2);

            var e = cmp ? this.MaleExpression : this.FemaleExpression;
            if (e is null)
            {
                bytesWritten = 0;
                return true;
            }

            return e.EvaluateToSpan(context, span, out bytesWritten);
        }

        throw new OutOfMemoryException("Temporary evaluation result exceeds 0x10000000 bytes");
    }
}
