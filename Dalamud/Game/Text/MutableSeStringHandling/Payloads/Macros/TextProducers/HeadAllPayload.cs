using System.Buffers;
using System.Text;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces a text with first characters of all words uppercased.</summary>
public sealed class HeadAllPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="HeadAllPayload"/> class.</summary>
    public HeadAllPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.HeadAll)
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

        var doUpper = true;
        for (var i = 0; i < bytesWritten;)
        {
            switch (Rune.DecodeFromUtf8(span[i..bytesWritten], out var rune, out var len))
            {
                case OperationStatus.Done:
                    if (doUpper)
                    {
                        rune = Rune.ToUpperInvariant(rune);
                        if (rune.Utf8SequenceLength != len)
                            break;

                        rune.EncodeToUtf8(span[i..bytesWritten]);
                    }

                    break;
                case OperationStatus.NeedMoreData:
                case OperationStatus.InvalidData:
                    break;
                case OperationStatus.DestinationTooSmall:
                default:
                    throw new InvalidOperationException();
            }

            doUpper = Rune.IsWhiteSpace(rune);
        }

        return true;
    }
}
