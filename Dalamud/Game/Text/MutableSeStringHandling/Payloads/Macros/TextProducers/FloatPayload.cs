using System.Globalization;
using System.Numerics;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a decimal text representation of a fractional number.</summary>
public sealed class FloatPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="FloatPayload"/> class.</summary>
    public FloatPayload()
        : base(3, 3, (int)Lumina.Text.Payloads.MacroCode.Float)
    {
    }

    /// <summary>Gets or sets the numerator.</summary>
    public IMutableSeExpression? Numerator
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the denominator.</summary>
    /// <remarks>The values are expected to be an exponential of 10.</remarks>
    public IMutableSeExpression? Denominator
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Gets or sets the decimal point.</summary>
    public IMutableSeExpression? DecimalPoint
    {
        get => this.ExpressionAt(2);
        set => this.ExpressionAt(2) = value;
    }

    /// <inheritdoc/>
    public override unsafe void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var bufStorage = default(Vector4);
        var buf = new Span<byte>(&bufStorage, sizeof(Vector4));
        if (!this.EvaluateToSpan(context, buf, out var len))
            throw new InvalidOperationException();
        ssb.Append(buf[..len]);
    }

    /// <inheritdoc/>
    public override unsafe bool EvaluateToSpan(
        ISeStringEvaluationContext context,
        Span<byte> span,
        out int bytesWritten)
    {
        var numerator = this.Numerator?.EvaluateAsInt(context) ?? 0;
        var denominator = this.Denominator?.EvaluateAsInt(context) ?? 0;
        if (denominator == 0)
            denominator = 1;

        var rpadStorage = default(Matrix4x4);
        var fractionalFormat = new Span<char>(&rpadStorage, sizeof(Matrix4x4) / sizeof(char));
        fractionalFormat = fractionalFormat[..Math.Max(1, (int)MathF.Ceiling(MathF.Log10(denominator)))];
        fractionalFormat.Fill('0');

        var (integerPart, fractionalPart) = int.DivRem(numerator, denominator);

        bytesWritten = 0;

        if (!integerPart.TryFormat(span[bytesWritten..], out var len))
            return false;
        bytesWritten += len;

        if (this.DecimalPoint?.EvaluateToSpan(context, span[bytesWritten..], out len) is false)
            return false;
        bytesWritten += len;

        if (!fractionalPart.TryFormat(span[bytesWritten..], out len, fractionalFormat))
            return false;
        bytesWritten += len;

        return true;
    }
}
