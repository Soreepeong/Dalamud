using System.Collections;

using LSeString = Lumina.Text.SeString;

namespace Dalamud.Game.Text.SeStringHandling.SeStringSpan;

/// <summary>Represents a SeString formed from a byte sequence.</summary>
public readonly ref struct SeStringReadOnlySpan
{
    /// <summary>The string data.</summary>
    public readonly ReadOnlySpan<byte> Body;

    /// <summary>Initializes a new instance of the <see cref="SeStringReadOnlySpan"/> struct.</summary>
    /// <param name="body">The backing data.</param>
    public SeStringReadOnlySpan(ReadOnlySpan<byte> body) => this.Body = body;

    /// <inheritdoc cref="SeStringReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeStringReadOnlySpan(Span<byte> body) => this.Body = body;

    /// <inheritdoc cref="SeStringReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeStringReadOnlySpan(ReadOnlyMemory<byte> body) => this.Body = body.Span;

    /// <inheritdoc cref="SeStringReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeStringReadOnlySpan(Memory<byte> body) => this.Body = body.Span;

    /// <inheritdoc cref="SeStringReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeStringReadOnlySpan(byte[] body) => this.Body = body;

    public static implicit operator ReadOnlySpan<byte>(SeStringReadOnlySpan s) => s.Body;

    public static implicit operator SeStringReadOnlySpan(ReadOnlySpan<byte> s) => new(s);

    public static implicit operator SeStringReadOnlySpan(Span<byte> s) => new(s);

    public static implicit operator SeStringReadOnlySpan(ReadOnlyMemory<byte> s) => new(s);

    public static implicit operator SeStringReadOnlySpan(Memory<byte> s) => new(s);

    public static implicit operator SeStringReadOnlySpan(byte[] s) => new(s);

    /// <summary>Gets an enumerator for enumerating over the payloads under this SeString.</summary>
    /// <returns>A new enumerator.</returns>
    public Enumerator GetEnumerator() => new(this.Body);

    /// <summary>Enumerator for <see cref="SeStringReadOnlySpan"/>.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<byte> data;
        private int index;

        /// <summary>Initializes a new instance of the <see cref="Enumerator"/> struct.</summary>
        /// <param name="data">The backing data.</param>
        public Enumerator(ReadOnlySpan<byte> data) => this.data = data;

        /// <inheritdoc cref="IEnumerator.Current"/>
        public SePayloadReadOnlySpan Current { get; private set; }

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext()
        {
            if (this.index >= this.data.Length)
                return false;

            this.index += this.Current.Envelope.Length;

            var subspan = this.data[this.index..];
            if (subspan.IsEmpty)
                return false;

            var i = 1;
            switch (subspan[0])
            {
                // A valid SeString never "contains" a null byte; it is always the terminator
                case 0:
                    this.Current = new(-1, this.data.Slice(this.index, 1), this.data.Slice(this.index, 1));
                    return true;

                case LSeString.StartByte:
                {
                    if (i == subspan.Length)
                    {
                        this.Current = new(-1, this.data.Slice(this.index, 1), this.data.Slice(this.index, 1));
                        return true;
                    }

                    // Payload type
                    var payloadType = subspan[i++];
                    if (i == subspan.Length)
                    {
                        this.Current = new(-1, this.data.Slice(this.index, 1), this.data.Slice(this.index, 1));
                        return true;
                    }

                    // Payload length
                    if (SeStringExpressionUtilities.TryDecodeInt(subspan[i..], out int bodyLength, out var exprLen))
                    {
                        i += exprLen;
                    }
                    else
                    {
                        this.Current = new(-1, this.data.Slice(this.index, 1), this.data.Slice(this.index, 1));
                        return true;
                    }

                    if (bodyLength < 0 || i + bodyLength + 1 > subspan.Length)
                    {
                        this.Current = new(-1, this.data.Slice(this.index, 1), this.data.Slice(this.index, 1));
                        return true;
                    }

                    var bodyOffset = i;
                    i += bodyLength;

                    if (subspan[i] != LSeString.EndByte)
                    {
                        this.Current = new(-1, this.data.Slice(this.index, 1), this.data.Slice(this.index, 1));
                        return true;
                    }

                    i++;
                    this.Current = new(payloadType, subspan[..i], subspan[bodyOffset..(i - 1)]);
                    return true;
                }

                default:
                {
                    while (i < subspan.Length && subspan[i] is not LSeString.StartByte and not 0)
                        i++;

                    this.Current = new(0, this.data.Slice(this.index, i), this.data.Slice(this.index, i));
                    return true;
                }
            }
        }
    }
}
