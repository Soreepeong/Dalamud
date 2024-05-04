using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a decimal text representation of an integer, delimited by thousands.</summary>
public sealed class KiloPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="KiloPayload"/> class.</summary>
    public KiloPayload()
        : base(2, 2, (int)Lumina.Text.Payloads.MacroCode.Kilo)
    {
    }

    /// <summary>Gets or sets the integer expression.</summary>
    public IMutableSeExpression? Value
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the separator expression.</summary>
    public IMutableSeExpression? Separator
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var value = this.Value?.EvaluateAsInt(context) ?? 0L;
        if (value < 0)
        {
            ssb.Append('-');
            value = -value;
        }

        var remainder = value;
        var display = false;
        for (int r = 1_000_000_000, i = 0; r > 1; r /= 10, i += 3)
        {
            (var d, remainder) = long.DivRem(remainder, r);
            if (!(display |= d > 0))
                continue;

            ssb.Append(d);
            if (i % 3 == 0)
                this.Separator?.EvaluateToSeStringBuilder(context, ssb);
        }

        ssb.Append(remainder);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var value = this.Value?.EvaluateAsInt(context) ?? 0L;
        bytesWritten = 0;
        if (value < 0)
        {
            if (bytesWritten >= span.Length)
                return false;
            span[bytesWritten++] = (byte)'-';
            value = -value;
        }

        var remainder = value;
        var display = false;
        for (int r = 1_000_000_000, i = 0; r > 1; r /= 10, i += 3)
        {
            (var d, remainder) = long.DivRem(remainder, r);
            if (!(display |= d > 0))
                continue;

            if (bytesWritten >= span.Length)
                return false;
            span[bytesWritten++] = (byte)('0' + d);
            if (i % 3 == 0 && this.Separator is not null)
            {
                if (!this.Separator.EvaluateToSpan(context, span[bytesWritten..], out var len2))
                    return false;
                bytesWritten += len2;
            }
        }

        return true;
    }
}
