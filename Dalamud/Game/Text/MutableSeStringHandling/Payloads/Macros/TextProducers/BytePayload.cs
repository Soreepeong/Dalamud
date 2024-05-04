using System.Globalization;
using System.Numerics;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a text representation of a storage size integer.</summary>
public sealed class BytePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="BytePayload"/> class.</summary>
    public BytePayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Byte)
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
        var v = this.Value?.EvaluateAsInt(context) ?? 0.0;
        var i = 0;
        while (v >= 1024 && i < 3)
        {
            v /= 1024;
            i++;
        }

        if (!v.TryFormat(span, out bytesWritten, "0.0", CultureInfo.InvariantCulture))
            return false;
        if (i > 0)
        {
            if (bytesWritten + 1 >= span.Length)
                return false;
            span[bytesWritten++] = " KMG"u8[i];
        }

        return true;
    }
}
