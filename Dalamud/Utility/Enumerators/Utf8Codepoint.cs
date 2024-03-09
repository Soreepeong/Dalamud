using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Dalamud.Utility.Enumerators;

/// <summary>Enumerates a UTF-8 byte sequence by codepoint.</summary>
public ref struct Utf8SpanEnumerator
{
    /// <summary>Represents an UTF-8 codepoint in a UTF-8 byte span.</summary>
    /// <param name="Offset">The offset of this codepoint, encoded in UTF-8..</param>
    /// <param name="Length">The length of this codepoint, encoded in UTF-8.</param>
    /// <param name="Codepoint">The raw codepoint value.</param>
    /// <param name="InvalidSequence">Whether the UTF-8 sequence was invalid. Codepoint may still not be a valid unicode
    /// codepoint value.</param>
    [SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1313:Parameter names should begin with lower-case letter",
        Justification = "no")]
    public readonly record struct Item(
        int Offset,
        int Length,
        int Codepoint,
        bool InvalidSequence)
    {
        /// <summary>Gets the effective <c>char</c> value, with invalid or non-representable codepoints replaced.
        /// </summary>
        public char EffectiveChar =>
            this.InvalidSequence || this.Codepoint is 0xFFFE or 0xFFFF
                ? '\uFFFD'
                : this.Codepoint >= 0x10000
                    ? '\u3013'
                    : (char)this.Codepoint;
    }

    private readonly ReadOnlySpan<byte> data;

    /// <summary>Initializes a new instance of the <see cref="Utf8SpanEnumerator"/> struct.</summary>
    /// <param name="data">The UTF-8 byte sequence.</param>
    public Utf8SpanEnumerator(ReadOnlySpan<byte> data) => this.data = data;

    /// <inheritdoc cref="IEnumerator.Current"/>
    public Item Current { get; private set; } = new(0, 0, 0, true);

    /// <inheritdoc cref="IEnumerator.MoveNext"/>
    public unsafe bool MoveNext()
    {
        var offset = this.Current.Offset + this.Current.Length;
        var subspan = this.data[offset..];
        if (subspan.IsEmpty)
            return false;

        fixed (byte* ptr = &this.data[offset])
        {
            if ((ptr[0] & 0x80) == 0)
            {
                this.Current = new(offset, 1, ptr[0], false);
            }
            else if ((ptr[0] & 0b11100000) == 0b11000000 && subspan.Length >= 2
                     && (ptr[1] & 0b11000000) == 0b10000000)
            {
                this.Current = new(
                    offset,
                    2,
                    ((ptr[0] & 0x1F) << 6) |
                    ((ptr[1] & 0x3F) << 0),
                    false);
            }
            else if ((ptr[0] & 0b11110000) == 0b11100000 && subspan.Length >= 3
                     && (ptr[1] & 0b11000000) == 0b10000000
                     && (ptr[2] & 0b11000000) == 0b10000000)
            {
                this.Current = new(
                    offset,
                    3,
                    ((ptr[0] & 0x0F) << 12) |
                    ((ptr[1] & 0x3F) << 6) |
                    ((ptr[2] & 0x3F) << 0),
                    false);
            }
            else if ((ptr[0] & 0b11111000) == 0b11110000 && subspan.Length >= 4
                     && (ptr[1] & 0b11000000) == 0b10000000
                     && (ptr[2] & 0b11000000) == 0b10000000
                     && (ptr[3] & 0b11000000) == 0b10000000)
            {
                this.Current = new(
                    offset,
                    4,
                    ((ptr[0] & 0x07) << 18) |
                    ((ptr[1] & 0x3F) << 12) |
                    ((ptr[2] & 0x3F) << 6) |
                    ((ptr[3] & 0x3F) << 0),
                    false);
            }
            else
            {
                this.Current = new(offset, 1, ptr[0], true);
            }

            return true;
        }
    }

    /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
    public Utf8SpanEnumerator GetEnumerator() => new(this.data);
}
