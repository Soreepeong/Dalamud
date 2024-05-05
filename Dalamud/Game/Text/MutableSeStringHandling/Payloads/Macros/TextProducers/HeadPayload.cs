using System.Buffers;
using System.Text;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a text with its first character uppercased.</summary>
public sealed class HeadPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="HeadPayload"/> class.</summary>
    public HeadPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Head)
    {
    }

    /// <summary>Gets or sets the string expression.</summary>
    public IMutableSeExpression? Value
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var arr = Array.Empty<byte>();
        for (var len = 4096; len < 0x10000000; len *= 2)
        {
            ArrayPool<byte>.Shared.Return(arr);

            arr = ArrayPool<byte>.Shared.Rent(len);
            if (this.EvaluateToSpan(context, arr, out var len1) is false)
                continue;
            ssb.Append(arr.AsSpan(0, len1));
            return;
        }

        throw new OutOfMemoryException("Temporary evaluation result exceeds 0x10000000 bytes");
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (this.Value is null)
        {
            bytesWritten = 0;
            return true;
        }

        if (!this.Value.EvaluateToSpan(context, span, out bytesWritten))
        {
            bytesWritten = 0;
            return false;
        }

        if (Rune.DecodeFromUtf8(span[..bytesWritten], out var rune, out var len) == OperationStatus.Done)
        {
            rune = Rune.ToUpperInvariant(rune);
            if (rune.Utf8SequenceLength == len)
                rune.EncodeToUtf8(span[..bytesWritten]);
        }

        return true;
    }
}
