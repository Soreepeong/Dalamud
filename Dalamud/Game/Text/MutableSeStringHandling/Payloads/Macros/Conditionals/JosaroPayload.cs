using System.Buffers;
using System.Text;
using System.Text.Unicode;

using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;

/// <summary>Tests a word to determine which josa to append, for ro/euro in particular.</summary>
public sealed class JosaroPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="JosaroPayload"/> class.</summary>
    public JosaroPayload()
        : base(3, 3, (int)Lumina.Text.Payloads.MacroCode.Josaro)
    {
    }

    /// <summary>Gets or sets the string expression to be suffixed with a josa.</summary>
    public IMutableSeExpression? Text
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="Text"/> ends with a trailing consonant jamo.
    /// </summary>
    public IMutableSeExpression? WithJongsungSuffix
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Gets or sets the expression to use when <see cref="Text"/> does not end with a trailing consonant jamo.
    /// </summary>
    public IMutableSeExpression? WithoutJongsungSuffix
    {
        get => this.ExpressionAt(2);
        set => this.ExpressionAt(2) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var arr = Array.Empty<byte>();
        for (var len = 4096; len < 0x10000000; len *= 2)
        {
            ArrayPool<byte>.Shared.Return(arr);

            arr = ArrayPool<byte>.Shared.Rent(len);
            var len1 = 0;
            if (this.Text?.EvaluateToSpan(context, arr, out len1) is false)
                continue;

            ssb.Append(new ReadOnlySeStringSpan(arr.AsSpan(0, len1)));
            if (len1 < 3 || Rune.DecodeFromUtf8(arr.AsSpan(len1 - 3, 3), out var rune, out _) != OperationStatus.Done)
            {
                ArrayPool<byte>.Shared.Return(arr);
                return;
            }

            ArrayPool<byte>.Shared.Return(arr);

            if (rune.Value < UnicodeRanges.HangulSyllables.FirstCodePoint
                || rune.Value >= UnicodeRanges.HangulSyllables.FirstCodePoint + UnicodeRanges.HangulSyllables.Length)
                return;

            var endsWithJongsung = (rune.Value - 0xAC00) % 28 is not 0 and not 8;
            var e = endsWithJongsung ? this.WithJongsungSuffix : this.WithoutJongsungSuffix;
            e?.EvaluateToSeStringBuilder(context, ssb);
            return;
        }

        throw new OutOfMemoryException("Temporary evaluation result exceeds 0x10000000 bytes");
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (this.Text?.EvaluateToSpan(context, span, out bytesWritten) is not true)
        {
            bytesWritten = 0;
            return true;
        }

        if (bytesWritten < 3)
            return true;

        if (Rune.DecodeFromUtf8(span[(bytesWritten - 3)..bytesWritten], out var rune, out _) != OperationStatus.Done)
            return true;

        if (rune.Value < UnicodeRanges.HangulSyllables.FirstCodePoint
            || rune.Value >= UnicodeRanges.HangulSyllables.FirstCodePoint + UnicodeRanges.HangulSyllables.Length)
            return true;

        var endsWithJongsung = (rune.Value - 0xAC00) % 28 is not 0 and not 8;
        var e = endsWithJongsung ? this.WithJongsungSuffix : this.WithoutJongsungSuffix;
        if (e is null)
        {
            bytesWritten = 0;
            return true;
        }

        if (!e.EvaluateToSpan(context, span[bytesWritten..], out var bytesWritten2))
            return false;

        bytesWritten += bytesWritten2;
        return true;
    }
}
