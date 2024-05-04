using System.Numerics;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a hexadecimal text representation of an integer.</summary>
public sealed class HexPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="HexPayload"/> class.</summary>
    public HexPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Hex)
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
        if (span.Length < 2)
        {
            bytesWritten = 0;
            return false;
        }

        if (this.Value is null)
        {
            if (span.Length < 3)
            {
                bytesWritten = 0;
                return false;
            }

            "0x0"u8.CopyTo(span);
            bytesWritten = 3;
            return true;
        }

        "0x"u8.CopyTo(span);
        if (this.Value.EvaluateAsInt(context).TryFormat(span[2..], out bytesWritten, "X"))
        {
            bytesWritten += 2;
            return true;
        }

        bytesWritten = 0;
        return false;
    }
}
