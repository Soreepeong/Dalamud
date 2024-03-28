using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;

using Dalamud.Interface.Spannables.Helpers;
using Dalamud.Interface.Spannables.Rendering;
using Dalamud.Utility.Numerics;

using ImGuiNET;

namespace Dalamud.Interface.Spannables.Text;

/// <summary>Base class for <see cref="TextSpannable"/> and <see cref="TextSpannableBuilder"/>.</summary>
public abstract partial class TextSpannableBase : ISpannable, ISpannableSerializable
{
    private static readonly BitArray WordBreakNormalBreakChars;

    static TextSpannableBase()
    {
        // Initialize which characters will make a valid word break point.

        WordBreakNormalBreakChars = new(char.MaxValue + 1);

        // https://en.wikipedia.org/wiki/Whitespace_character
        foreach (var c in
                 "\t\n\v\f\r\x20\u0085\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2008\u2009\u200A\u2028\u2029\u205F\u3000\u180E\u200B\u200C\u200D")
            WordBreakNormalBreakChars[c] = true;

        foreach (var range in new[]
                 {
                     UnicodeRanges.HangulJamo,
                     UnicodeRanges.HangulSyllables,
                     UnicodeRanges.HangulCompatibilityJamo,
                     UnicodeRanges.HangulJamoExtendedA,
                     UnicodeRanges.HangulJamoExtendedB,
                     UnicodeRanges.CjkCompatibility,
                     UnicodeRanges.CjkCompatibilityForms,
                     UnicodeRanges.CjkCompatibilityIdeographs,
                     UnicodeRanges.CjkRadicalsSupplement,
                     UnicodeRanges.CjkSymbolsandPunctuation,
                     UnicodeRanges.CjkStrokes,
                     UnicodeRanges.CjkUnifiedIdeographs,
                     UnicodeRanges.CjkUnifiedIdeographsExtensionA,
                     UnicodeRanges.Hiragana,
                     UnicodeRanges.Katakana,
                     UnicodeRanges.KatakanaPhoneticExtensions,
                 })
        {
            for (var i = 0; i < range.Length; i++)
                WordBreakNormalBreakChars[range.FirstCodePoint + i] = true;
        }
    }

    /// <inheritdoc/>
    public event Action<ISpannable>? SpannableChange;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var s in this.GetAllChildSpannables())
            s?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public abstract IReadOnlyCollection<ISpannable?> GetAllChildSpannables();

    /// <inheritdoc/>
    public int SerializeState(Span<byte> buffer) =>
        SpannableSerializationHelper.Write(ref buffer, this.GetAllChildSpannables());

    /// <inheritdoc/>
    public bool TryDeserializeState(ReadOnlySpan<byte> buffer, out int consumed)
    {
        var origLen = buffer.Length;
        consumed = 0;
        if (!SpannableSerializationHelper.TryRead(ref buffer, this.GetAllChildSpannables()))
            return false;
        consumed += origLen - buffer.Length;
        return true;
    }

    /// <summary>Gets the data required for rendering.</summary>
    /// <returns>The data.</returns>
    private protected abstract DataRef GetData();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEffectivelyInfinity(float f) => f >= float.PositiveInfinity;

    private ref struct StateInfo
    {
        public float HorizontalOffsetWrtLine;
        public float VerticalOffsetWrtLine;

        private readonly IInternalMeasurement mm;
        private readonly Vector2 lineBBoxVertical;
        private readonly float lineWidth;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StateInfo(IInternalMeasurement mm, scoped in MeasuredLine lineMeasurement)
        {
            this.mm = mm;
            this.lineBBoxVertical = lineMeasurement.BBoxVertical;
            this.lineWidth = lineMeasurement.Width;
        }

        public void Update(in TextStyleFontData fontInfo)
        {
            var lineAscentDescent = this.lineBBoxVertical;
            this.VerticalOffsetWrtLine = (fontInfo.BBoxVertical.Y - fontInfo.BBoxVertical.X) *
                                         this.mm.LastStyle.VerticalOffset;
            switch (this.mm.LastStyle.VerticalAlignment)
            {
                case < 0:
                    this.VerticalOffsetWrtLine -= lineAscentDescent.X + (fontInfo.Font.Ascent * fontInfo.Scale);
                    break;
                case >= 1f:
                    this.VerticalOffsetWrtLine += lineAscentDescent.Y - lineAscentDescent.X - fontInfo.ScaledFontSize;
                    break;
                default:
                    this.VerticalOffsetWrtLine +=
                        (lineAscentDescent.Y - lineAscentDescent.X - fontInfo.ScaledFontSize) *
                        this.mm.LastStyle.VerticalAlignment;
                    break;
            }

            this.VerticalOffsetWrtLine = MathF.Round(this.VerticalOffsetWrtLine * fontInfo.Scale) / fontInfo.Scale;

            var alignWidth = this.mm.Options.Size.X;
            var alignLeft = 0f;
            if (IsEffectivelyInfinity(alignWidth))
            {
                if (!this.mm.Boundary.IsValid)
                {
                    this.HorizontalOffsetWrtLine = 0;
                    return;
                }

                alignWidth = this.mm.Boundary.Width;
                alignLeft = this.mm.Boundary.Left;
            }

            switch (this.mm.LastStyle.HorizontalAlignment)
            {
                case <= 0f:
                    this.HorizontalOffsetWrtLine = 0;
                    break;

                case >= 1f:
                    this.HorizontalOffsetWrtLine = alignLeft + (alignWidth - this.lineWidth);
                    break;

                default:
                    this.HorizontalOffsetWrtLine =
                        MathF.Round(
                            (alignLeft + (alignWidth - this.lineWidth)) *
                            this.mm.LastStyle.HorizontalAlignment *
                            fontInfo.Scale)
                        / fontInfo.Scale;
                    break;
            }
        }
    }

    /// <summary>Raises the <see cref="SpannableChange"/> event.</summary>
    /// <param name="obj">The spannable that has been changed.</param>
    protected virtual void OnSpannableChange(ISpannable obj) => this.SpannableChange?.Invoke(obj);

    [StructLayout(LayoutKind.Sequential)]
    private struct BoundaryToRecord
    {
        public RectVector4 Boundary;
        public int RecordIndex;

        public BoundaryToRecord(int recordIndex, RectVector4 boundary)
        {
            this.RecordIndex = recordIndex;
            this.Boundary = boundary;
        }
    }
    
    /// <summary>Struct for storing link interaction data.</summary>
    private struct LinkInteractionData
    {
        public bool IsMouseButtonDownHandled;
        public ImGuiMouseButton FirstMouseButton;
    }
}
