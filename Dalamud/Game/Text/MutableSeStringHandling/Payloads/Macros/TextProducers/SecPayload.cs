using System.Globalization;
using System.Numerics;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a decimal text representation of an integer, padded to two digits.
/// Excess significant digits are omitted.</summary>
public sealed class SecPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="SecPayload"/> class.</summary>
    public SecPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Sec)
    {
    }

    /// <summary>Gets or sets the integer expression.</summary>
    public IMutableSeExpression? Value
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
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
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var v = (this.Value?.EvaluateAsInt(context) ?? 0) % 100;
        if (v < 0) v += 100;

        return v.TryFormat(span, out bytesWritten, "00", CultureInfo.InvariantCulture);
    }
}
